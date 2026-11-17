namespace Servia.Domain.Ai;

/// <summary>
/// Resolve o identificador opaco do tenant enviado ao provedor de IA
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// O identificador é gerado uma vez e persistido; as chamadas seguintes
/// reutilizam o valor gravado. Isso mantém a identificação estável perante o
/// provedor e permite rotação rastreável.
///
/// O contrato vive no domínio para que os provedores de IA possam depender
/// dele sem conhecer a camada de aplicação.
/// </summary>
public interface IAiTenantIdentifierResolver
{
    /// <summary>
    /// Retorna o identificador opaco do tenant, criando e persistindo na
    /// primeira utilização.
    /// </summary>
    Task<string> ResolveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
