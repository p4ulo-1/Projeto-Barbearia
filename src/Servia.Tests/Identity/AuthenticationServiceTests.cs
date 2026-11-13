using Servia.Application.Identity;
using Servia.Domain.Identity;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Tenancy;

using Servia.Tests.Common;

namespace Servia.Tests.Identity;

/// <summary>
/// F10.1 — login com e-mail e senha.
/// F10.6 — refresh token com rotação.
/// F10.7 — logout com revogação.
/// </summary>
public class AuthenticationServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private const string Password = "SenhaSegura123";

    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _tokens = new();
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);
    private readonly FixedTimeProvider _time = new(Now);
    private readonly TenantContext _tenantContext = new();
    private readonly FakeCustomRoleRepository _customRoles = new();

    private AuthenticationService NewService() =>
        new(_users, _tokens, _hasher, new FakeTokenIssuer(), _time, _tenantContext, _customRoles);

    private User SeedUser(Role role = Role.Owner, bool active = true)
    {
        var user = User.CreateForTenant(
            TenantId, "Ana Souza", "ana@exemplo.com", _hasher.Hash(Password), role, Now);

        if (!active)
        {
            user.Deactivate();
        }

        _users.Seed(user);
        return user;
    }

    private SignInCommand Command(string email = "ana@exemplo.com", string password = Password) =>
        new(TenantId, email, password);

    // ---------- F10.1: login ----------

    [Fact]
    public async Task Login_Com_Credenciais_Validas_Emite_Tokens()
    {
        SeedUser();

        var result = await NewService().SignInAsync(Command());

        Assert.True(result.Success);
        Assert.NotNull(result.AccessToken);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    [Fact]
    public async Task Login_Com_Senha_Errada_Falha()
    {
        SeedUser();

        var result = await NewService().SignInAsync(Command(password: "SenhaErrada123"));

        Assert.False(result.Success);
        Assert.Null(result.AccessToken);
    }

    [Fact]
    public async Task Login_Com_Email_Inexistente_Falha_Com_A_Mesma_Mensagem()
    {
        SeedUser();

        var wrongPassword = await NewService().SignInAsync(Command(password: "Errada123"));
        var unknownEmail = await NewService().SignInAsync(Command(email: "naoexiste@exemplo.com"));

        // Mesma mensagem: nao revela quais e-mails existem.
        Assert.Equal(wrongPassword.FailureReason, unknownEmail.FailureReason);
    }

    [Fact]
    public async Task Login_De_Usuario_Inativo_Falha()
    {
        SeedUser(active: false);

        var result = await NewService().SignInAsync(Command());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Login_Sem_Tenant_Falha()
    {
        SeedUser();

        var result = await NewService().SignInAsync(new SignInCommand(Guid.Empty, "ana@exemplo.com", Password));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Login_Registra_O_Instante_Do_Acesso()
    {
        var user = SeedUser();

        await NewService().SignInAsync(Command());

        Assert.Equal(Now, user.LastLoginAt);
    }

    [Fact]
    public async Task Login_Persiste_O_Token_De_Renovacao()
    {
        SeedUser();

        await NewService().SignInAsync(Command());

        Assert.Single(_tokens.Stored);
    }

    [Fact]
    public async Task Token_De_Renovacao_Nao_Eh_Persistido_Em_Claro()
    {
        SeedUser();

        var result = await NewService().SignInAsync(Command());

        // O banco guarda o hash; o valor em claro so vai para o cliente.
        Assert.DoesNotContain(_tokens.Stored, token => token.TokenHash == result.RefreshToken);
    }

    // ---------- F10.6: rotacao ----------

    [Fact]
    public async Task Renovar_Emite_Novos_Tokens()
    {
        SeedUser();
        var service = NewService();
        var signIn = await service.SignInAsync(Command());

        var refreshed = await service.RefreshAsync(signIn.RefreshToken!);

        Assert.True(refreshed.Success);
        Assert.NotEqual(signIn.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Renovar_Revoga_O_Token_Anterior()
    {
        SeedUser();
        var service = NewService();
        var signIn = await service.SignInAsync(Command());

        await service.RefreshAsync(signIn.RefreshToken!);

        // O token antigo nao pode ser usado de novo.
        var reuse = await service.RefreshAsync(signIn.RefreshToken!);

        Assert.False(reuse.Success);
    }

    [Fact]
    public async Task Renovar_Com_Token_Invalido_Falha()
    {
        SeedUser();

        var result = await NewService().RefreshAsync("token-inexistente");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Renovar_Com_Token_Expirado_Falha()
    {
        SeedUser();
        var service = NewService();
        var signIn = await service.SignInAsync(Command());

        // Avanca alem da validade do token de renovacao.
        _time.Advance(TimeSpan.FromDays(RefreshToken.DefaultLifetimeDays + 1));

        var result = await service.RefreshAsync(signIn.RefreshToken!);

        Assert.False(result.Success);
    }

    // ---------- F10.7: logout ----------

    [Fact]
    public async Task Logout_Revoga_O_Token()
    {
        SeedUser();
        var service = NewService();
        var signIn = await service.SignInAsync(Command());

        var signedOut = await service.SignOutAsync(signIn.RefreshToken!);

        Assert.True(signedOut);

        var reuse = await service.RefreshAsync(signIn.RefreshToken!);
        Assert.False(reuse.Success);
    }

    [Fact]
    public async Task Logout_Com_Token_Inexistente_Retorna_Falso()
    {
        SeedUser();

        Assert.False(await NewService().SignOutAsync("token-inexistente"));
    }

    [Fact]
    public async Task Logout_Eh_Idempotente()
    {
        SeedUser();
        var service = NewService();
        var signIn = await service.SignInAsync(Command());

        await service.SignOutAsync(signIn.RefreshToken!);
        var second = await service.SignOutAsync(signIn.RefreshToken!);

        // Repetir o logout nao lanca nem falha de forma inesperada.
        Assert.True(second);
    }
}

/// <summary>Repositório de usuários em memória.</summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public void Seed(User user) => _users.Add(user);

    public Task<User?> FindByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(
            user => user.TenantId == tenantId && user.Email == email.ToLowerInvariant()));

    public Task<User?> FindByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.FirstOrDefault(
            user => user.TenantId == tenantId && user.Id == userId));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Repositório de tokens em memória.</summary>
internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];

    public IReadOnlyList<RefreshToken> Stored => _tokens;

    public Task<RefreshToken?> FindByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokens.FirstOrDefault(
            token => token.TenantId == tenantId && token.TokenHash == tokenHash));

    public Task<RefreshToken?> FindByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        foreach (var token in _tokens.Where(
            token => token.TenantId == tenantId && token.UserId == userId))
        {
            token.RevokeIfActive(now, reason);
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Emissor de token falso, para testes determinísticos.</summary>
internal sealed class FakeTokenIssuer : ITokenIssuer
{
    /// <summary>Últimas permissões efetivas recebidas, para asserção.</summary>
    public IReadOnlySet<Permission>? LastPermissions { get; private set; }

    public AccessToken Issue(User user, DateTimeOffset now) =>
        Issue(user, RolePermissions.For(user.Role), now);

    public AccessToken Issue(
        User user,
        IReadOnlySet<Permission> effectivePermissions,
        DateTimeOffset now)
    {
        LastPermissions = effectivePermissions;

        return new($"token-{user.Id}-{now.ToUnixTimeSeconds()}", now.AddMinutes(15));
    }
}

/// <summary>Provedor de tempo controlável.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

/// <summary>Repositório de papéis personalizados em memória.</summary>
internal sealed class FakeCustomRoleRepository : ICustomRoleRepository
{
    private readonly List<CustomRole> _roles = [];

    public void Add(CustomRole role) => _roles.Add(role);

    public Task<IReadOnlyList<CustomRole>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CustomRole>>(
            _roles.Where(role => role.TenantId == tenantId).ToList());

    public Task<CustomRole?> FindAsync(
        Guid tenantId, Guid customRoleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_roles.FirstOrDefault(
            role => role.TenantId == tenantId && role.Id == customRoleId));

    public Task AddAsync(CustomRole role, CancellationToken cancellationToken = default)
    {
        _roles.Add(role);
        return Task.CompletedTask;
    }

    public void Remove(CustomRole role) => _roles.Remove(role);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
