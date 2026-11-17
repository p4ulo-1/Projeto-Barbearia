namespace Servia.Domain.Ai;

/// <summary>
/// Identificador opaco persistido de um tenant perante o provedor de IA
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// <b>Por que persistir:</b> o identificador é gerado uma vez e reutilizado nas
/// chamadas seguintes. Recalculá-lo a cada operação impediria a rotação
/// rastreável e tornaria a identificação instável se a chave mudasse.
///
/// <b>Rotação normal:</b> novos identificadores usam a versão corrente da
/// chave. Os já persistidos permanecem estáveis e continuam válidos — a chave
/// anterior deixa de gerar novos valores, mas não invalida os existentes.
///
/// <b>Rotação emergencial:</b> quando a chave é comprometida, os identificadores
/// afetados podem ser regenerados. O registro preserva a versão anterior e a
/// nova, além do momento da regeneração, para que a perda de continuidade de
/// identificação perante o provedor fique explícita.
/// </summary>
public sealed class AiTenantIdentifier
{
    private AiTenantIdentifier(
        Guid id,
        Guid tenantId,
        string value,
        string keyVersion,
        DateTimeOffset createdAt,
        string? previousValue,
        string? previousKeyVersion,
        DateTimeOffset? rotatedAt)
    {
        Id = id;
        TenantId = tenantId;
        Value = value;
        KeyVersion = keyVersion;
        CreatedAt = createdAt;
        PreviousValue = previousValue;
        PreviousKeyVersion = previousKeyVersion;
        RotatedAt = rotatedAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private AiTenantIdentifier()
    {
        Value = string.Empty;
        KeyVersion = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// Valor opaco enviado ao provedor. Não contém dados pessoais nem o
    /// segredo que o originou.
    /// </summary>
    public string Value { get; private set; }

    /// <summary>Versão da chave HMAC que gerou o valor corrente.</summary>
    public string KeyVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Valor anterior, preservado após rotação emergencial. Nulo quando nunca
    /// houve rotação.
    /// </summary>
    public string? PreviousValue { get; private set; }

    /// <summary>Versão da chave do valor anterior.</summary>
    public string? PreviousKeyVersion { get; private set; }

    /// <summary>Momento da última rotação emergencial.</summary>
    public DateTimeOffset? RotatedAt { get; private set; }

    /// <summary>Indica se o identificador já foi regenerado alguma vez.</summary>
    public bool WasRotated => RotatedAt.HasValue;

    public static AiTenantIdentifier Create(
        Guid tenantId,
        string value,
        string keyVersion,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new Common.DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Common.DomainRuleException("O identificador opaco é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(keyVersion))
        {
            throw new Common.DomainRuleException("A versão da chave é obrigatória.");
        }

        return new AiTenantIdentifier(
            Guid.NewGuid(),
            tenantId,
            value.Trim(),
            keyVersion.Trim(),
            now,
            previousValue: null,
            previousKeyVersion: null,
            rotatedAt: null);
    }

    /// <summary>
    /// Regenera o identificador por rotação emergencial. O valor e a versão
    /// anteriores são preservados para auditoria; a perda de continuidade de
    /// identificação perante o provedor fica registrada em <see cref="RotatedAt"/>.
    /// </summary>
    public void Rotate(string newValue, string newKeyVersion, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            throw new Common.DomainRuleException("O novo identificador é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(newKeyVersion))
        {
            throw new Common.DomainRuleException("A nova versão da chave é obrigatória.");
        }

        PreviousValue = Value;
        PreviousKeyVersion = KeyVersion;
        Value = newValue.Trim();
        KeyVersion = newKeyVersion.Trim();
        RotatedAt = now;
    }
}
