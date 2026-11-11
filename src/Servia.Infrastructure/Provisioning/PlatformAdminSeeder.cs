using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Provisioning;

/// <summary>
/// Cria o primeiro administrador da plataforma (F20.1).
///
/// Sem isso, colocar o primeiro cliente no ar exige calcular um hash
/// PBKDF2 à mão e inserir no banco — operação que erra com facilidade e
/// não deixa rastro.
///
/// <b>Idempotente:</b> se já existe um administrador, não cria outro.
/// Rodar duas vezes não duplica nem falha.
///
/// <b>Não é endpoint:</b> é um comando de linha. Um endpoint público para
/// criar administrador seria uma porta de escalada de privilégio.
/// </summary>
public sealed class PlatformAdminSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Garante que exista um administrador da plataforma.
    /// Retorna o e-mail criado, ou nulo quando já existia.
    /// </summary>
    public async Task<string?> SeedAsync(
        string name,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do administrador é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainRuleException("O e-mail do administrador é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new DomainRuleException("A senha deve ter ao menos 8 caracteres.");
        }

        // O administrador da plataforma nao pertence a um tenant: a busca
        // precisa ignorar o filtro global.
        var existing = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Role == Role.PlatformAdmin, cancellationToken);

        if (existing)
        {
            return null;
        }

        var admin = User.CreatePlatformAdmin(
            name.Trim(),
            email.Trim().ToLowerInvariant(),
            passwordHasher.Hash(password),
            timeProvider.GetUtcNow());

        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);

        return admin.Email;
    }
}
