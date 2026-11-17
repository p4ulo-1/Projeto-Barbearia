using Servia.Application.Ai;
using Servia.Domain.Ai;
using Servia.Domain.Common;

namespace Servia.Tests.Ai;

/// <summary>
/// Resolução e persistência do identificador opaco do tenant
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// O identificador é gerado uma vez, persistido e reutilizado. A rotação
/// normal preserva os valores existentes; a rotação emergencial regenera e
/// registra a versão anterior.
/// </summary>
public class AiTenantIdentifierServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeIdentifierRepository _repository = new();

    private AiTenantIdentifierService NewService(string version = "v1") =>
        new(_repository, new FakeIdentifierOptions("segredo-teste", version), TimeProvider.System);

    // ---------- Geração e reutilização ----------

    [Fact]
    public async Task Gera_E_Persiste_Na_Primeira_Utilizacao()
    {
        var value = await NewService().ResolveAsync(TenantId);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Single(_repository.Items);
        Assert.Equal(TenantId, _repository.Items[0].TenantId);
        Assert.Equal("v1", _repository.Items[0].KeyVersion);
    }

    [Fact]
    public async Task Reutiliza_O_Valor_Persistido()
    {
        var service = NewService();

        var first = await service.ResolveAsync(TenantId);
        var second = await service.ResolveAsync(TenantId);

        Assert.Equal(first, second);
        Assert.Single(_repository.Items);
    }

    [Fact]
    public async Task Valor_Corresponde_Ao_Hmac_Do_Tenant()
    {
        var value = await NewService().ResolveAsync(TenantId);

        Assert.Equal(AiUserIdentifier.ForTenant(TenantId, "segredo-teste", "v1"), value);
    }

    [Fact]
    public async Task Tenants_Diferentes_Recebem_Valores_Diferentes()
    {
        var service = NewService();
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.NotEqual(
            await service.ResolveAsync(TenantId),
            await service.ResolveAsync(other));
    }

    // ---------- Rotação normal ----------

    [Fact]
    public async Task Rotacao_Normal_Preserva_Identificadores_Existentes()
    {
        var original = await NewService("v1").ResolveAsync(TenantId);

        // Nova versao da chave: o tenant existente mantem o valor gravado.
        var afterRotation = await NewService("v2").ResolveAsync(TenantId);

        Assert.Equal(original, afterRotation);
        Assert.Equal("v1", _repository.Items[0].KeyVersion);
    }

    [Fact]
    public async Task Rotacao_Normal_Aplica_Nova_Versao_A_Tenants_Novos()
    {
        await NewService("v1").ResolveAsync(TenantId);

        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var value = await NewService("v2").ResolveAsync(other);

        Assert.StartsWith("tenant_v2_", value, StringComparison.Ordinal);
    }

    // ---------- Rotação emergencial ----------

    [Fact]
    public async Task Rotacao_Emergencial_Regenera_E_Preserva_Anterior()
    {
        var service = NewService("v1");
        var original = await service.ResolveAsync(TenantId);

        var rotated = await service.RotateAsync(TenantId);

        var item = _repository.Items[0];

        Assert.NotEqual(original, rotated);
        Assert.Equal(original, item.PreviousValue);
        Assert.Equal("v1", item.PreviousKeyVersion);
        Assert.NotNull(item.RotatedAt);
        Assert.True(item.WasRotated);
    }

    [Fact]
    public async Task Rotacao_Emergencial_Em_Tenant_Sem_Identificador_Cria_Um()
    {
        var value = await NewService().RotateAsync(TenantId);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Single(_repository.Items);
        Assert.False(_repository.Items[0].WasRotated);
    }

    // ---------- Invariantes ----------

    [Fact]
    public void Identificador_Exige_Tenant()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiTenantIdentifier.Create(Guid.Empty, "valor", "v1", Now));
    }

    [Fact]
    public void Identificador_Exige_Valor()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiTenantIdentifier.Create(TenantId, "  ", "v1", Now));
    }

    [Fact]
    public void Identificador_Exige_Versao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiTenantIdentifier.Create(TenantId, "valor", "", Now));
    }
}

/// <summary>Repositório de identificadores em memória.</summary>
internal sealed class FakeIdentifierRepository : IAiTenantIdentifierRepository
{
    public List<AiTenantIdentifier> Items { get; } = [];

    public Task<AiTenantIdentifier?> FindAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(item => item.TenantId == tenantId));

    public Task AddAsync(
        AiTenantIdentifier identifier, CancellationToken cancellationToken = default)
    {
        Items.Add(identifier);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>Configuração do identificador para teste.</summary>
internal sealed class FakeIdentifierOptions(string secret, string keyVersion) : IAiIdentifierOptions
{
    public string Secret => secret;

    public string KeyVersion => keyVersion;
}
