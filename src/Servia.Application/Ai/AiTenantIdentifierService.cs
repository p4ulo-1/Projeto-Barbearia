using Servia.Domain.Ai;

namespace Servia.Application.Ai;

/// <summary>
/// Resolve o identificador opaco do tenant enviado ao provedor de IA
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// <b>Gera uma vez, reutiliza sempre:</b> o identificador é criado na primeira
/// operação do tenant e persistido. As chamadas seguintes reutilizam o valor
/// gravado, sem recalculá-lo — isso mantém a identificação estável perante o
/// provedor e permite rotação rastreável.
///
/// <b>Rotação normal:</b> quando a versão da chave muda, os identificadores já
/// persistidos continuam válidos. Apenas tenants novos recebem a nova versão.
///
/// <b>Rotação emergencial:</b> <see cref="RotateAsync"/> regenera o valor com a
/// versão corrente, preservando o anterior e registrando o momento — a perda de
/// continuidade de identificação fica explícita.
///
/// <b>Sem PII:</b> o valor é derivado por HMAC do identificador interno; o
/// segredo nunca é exposto.
/// </summary>
public sealed class AiTenantIdentifierService(
    IAiTenantIdentifierRepository repository,
    IAiIdentifierOptions options,
    TimeProvider timeProvider) : IAiTenantIdentifierResolver
{
    /// <summary>
    /// Retorna o identificador opaco do tenant, criando e persistindo na
    /// primeira utilização.
    /// </summary>
    public async Task<string> ResolveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.FindAsync(tenantId, cancellationToken);

        if (existing is not null)
        {
            return existing.Value;
        }

        var value = AiUserIdentifier.ForTenant(
            tenantId, options.Secret, options.KeyVersion);

        var identifier = AiTenantIdentifier.Create(
            tenantId, value, options.KeyVersion, timeProvider.GetUtcNow());

        await repository.AddAsync(identifier, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return identifier.Value;
    }

    /// <summary>
    /// Regenera o identificador do tenant por rotação emergencial. O valor e a
    /// versão anteriores são preservados para auditoria.
    /// </summary>
    public async Task<string> RotateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.FindAsync(tenantId, cancellationToken);

        if (existing is null)
        {
            return await ResolveAsync(tenantId, cancellationToken);
        }

        // O nonce garante um valor novo mesmo com a mesma chave e versao.
        var nonce = Guid.NewGuid().ToString("N");

        var value = AiUserIdentifier.ForTenantRotated(
            tenantId, options.Secret, options.KeyVersion, nonce);

        existing.Rotate(value, options.KeyVersion, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        return existing.Value;
    }
}
