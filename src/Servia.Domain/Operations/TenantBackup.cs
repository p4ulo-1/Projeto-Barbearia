using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Operations;

/// <summary>
/// Backup dos dados de um estabelecimento (F9.3).
///
/// O backup é <b>por tenant</b>: restaurar um estabelecimento não afeta os
/// demais. Isso é consequência direta do isolamento multi-tenant (ADR-016).
///
/// A restauração exige que o backup esteja íntegro e que o tenant de destino
/// seja o mesmo da origem — restaurar dados de um tenant em outro seria uma
/// violação de isolamento.
/// </summary>
public sealed class TenantBackup : ITenantScoped
{
    private TenantBackup(
        Guid id,
        Guid tenantId,
        string storageLocation,
        long sizeInBytes,
        string checksum,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        StorageLocation = storageLocation;
        SizeInBytes = sizeInBytes;
        Checksum = checksum;
        CreatedAt = createdAt;
        Status = BackupStatus.Completed;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private TenantBackup()
    {
        StorageLocation = string.Empty;
        Checksum = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Local onde o backup está armazenado.</summary>
    public string StorageLocation { get; private set; }

    public long SizeInBytes { get; private set; }

    /// <summary>Hash do conteúdo, para verificação de integridade.</summary>
    public string Checksum { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public BackupStatus Status { get; private set; }

    public DateTimeOffset? RestoredAt { get; private set; }

    /// <summary>Indica se o backup pode ser restaurado.</summary>
    public bool CanRestore => Status == BackupStatus.Completed;

    public static TenantBackup Create(
        Guid tenantId,
        string storageLocation,
        long sizeInBytes,
        string checksum,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new DomainRuleException("O local de armazenamento é obrigatório.");
        }

        if (sizeInBytes <= 0)
        {
            throw new DomainRuleException("O tamanho do backup deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new DomainRuleException("O checksum é obrigatório para verificação de integridade.");
        }

        return new TenantBackup(
            Guid.NewGuid(), tenantId, storageLocation.Trim(), sizeInBytes, checksum.Trim(), now);
    }

    /// <summary>
    /// Restaura o backup no tenant de destino.
    /// O destino precisa ser o mesmo tenant da origem.
    /// </summary>
    public void Restore(Guid targetTenantId, DateTimeOffset now)
    {
        if (!CanRestore)
        {
            throw new DomainRuleException("Somente backups concluídos podem ser restaurados.");
        }

        if (targetTenantId != TenantId)
        {
            throw new DomainRuleException(
                "O backup só pode ser restaurado no próprio estabelecimento de origem.");
        }

        RestoredAt = now;
    }

    /// <summary>Marca o backup como corrompido, impedindo restauração.</summary>
    public void MarkCorrupted() => Status = BackupStatus.Corrupted;
}

/// <summary>Estado do backup.</summary>
public enum BackupStatus
{
    Completed = 1,
    Corrupted = 2
}
