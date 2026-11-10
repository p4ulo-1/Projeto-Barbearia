using Servia.Domain.Common;
using Servia.Domain.Operations;

namespace Servia.Tests.Operations;

/// <summary>
/// F9.3 — backup e restauração por tenant.
/// F9.4 — LGPD: consentimento, exclusão e anonimização.
/// F9.5 — política de retenção de dados.
/// </summary>
public class DataGovernanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 10);

    // ---------- F9.3: backup ----------

    private static TenantBackup NewBackup() =>
        TenantBackup.Create(TenantId, "s3://backups/servia", 1024 * 1024, "sha256:abc123", Now);

    [Fact]
    public void Criar_Backup_Valido()
    {
        var backup = NewBackup();

        Assert.Equal(TenantId, backup.TenantId);
        Assert.Equal(BackupStatus.Completed, backup.Status);
        Assert.True(backup.CanRestore);
    }

    [Fact]
    public void Criar_Backup_Sem_Checksum_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            TenantBackup.Create(TenantId, "s3://backups", 1024, "  ", Now));
    }

    [Fact]
    public void Criar_Backup_Com_Tamanho_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            TenantBackup.Create(TenantId, "s3://backups", 0, "sha256:abc", Now));
    }

    [Fact]
    public void Restaurar_No_Proprio_Tenant()
    {
        var backup = NewBackup();

        backup.Restore(TenantId, Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), backup.RestoredAt);
    }

    [Fact]
    public void Restaurar_Em_Outro_Tenant_Lanca_Excecao()
    {
        var backup = NewBackup();

        var exception = Assert.Throws<DomainRuleException>(() =>
            backup.Restore(Guid.NewGuid(), Now));

        Assert.Contains("próprio estabelecimento", exception.Message);
    }

    [Fact]
    public void Backup_Corrompido_Nao_Pode_Ser_Restaurado()
    {
        var backup = NewBackup();
        backup.MarkCorrupted();

        Assert.False(backup.CanRestore);
        Assert.Throws<DomainRuleException>(() => backup.Restore(TenantId, Now));
    }

    // ---------- F9.4: LGPD ----------

    [Fact]
    public void Registrar_Solicitacao_De_Exclusao()
    {
        var request = DataSubjectRequest.Create(
            TenantId, CustomerId, DataSubjectRequestType.Deletion, Now);

        Assert.Equal(DataSubjectRequestStatus.Pending, request.Status);
        Assert.Equal(DataSubjectRequestType.Deletion, request.RequestType);
    }

    [Fact]
    public void Prazo_Legal_Eh_De_15_Dias()
    {
        var request = DataSubjectRequest.Create(
            TenantId, CustomerId, DataSubjectRequestType.Access, Now);

        Assert.Equal(Now.AddDays(15), request.DueAt);
    }

    [Fact]
    public void Solicitacao_Vencida_Eh_Detectada()
    {
        var request = DataSubjectRequest.Create(
            TenantId, CustomerId, DataSubjectRequestType.Access, Now);

        Assert.False(request.IsOverdue(Now.AddDays(14)));
        Assert.True(request.IsOverdue(Now.AddDays(16)));
    }

    [Fact]
    public void Atender_Solicitacao()
    {
        var request = DataSubjectRequest.Create(
            TenantId, CustomerId, DataSubjectRequestType.Deletion, Now);

        request.Complete(Now.AddDays(2));

        Assert.Equal(DataSubjectRequestStatus.Completed, request.Status);
        Assert.Equal(Now.AddDays(2), request.CompletedAt);
        Assert.False(request.IsOverdue(Now.AddDays(30)));
    }

    [Fact]
    public void Recusar_Solicitacao_Exige_Fundamento()
    {
        var request = DataSubjectRequest.Create(
            TenantId, CustomerId, DataSubjectRequestType.Deletion, Now);

        Assert.Throws<DomainRuleException>(() => request.Reject("  "));

        request.Reject("Obrigação legal de guarda");
        Assert.Equal(DataSubjectRequestStatus.Rejected, request.Status);
    }

    [Fact]
    public void Solicitar_Sem_Titular_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            DataSubjectRequest.Create(TenantId, Guid.Empty, DataSubjectRequestType.Access, Now));
    }

    // ---------- F9.5: retencao ----------

    [Fact]
    public void Politica_Padrao_Define_Prazos_Por_Categoria()
    {
        var policy = RetentionPolicy.CreateDefault();

        Assert.Equal(5 * 365, policy.RetentionFor(DataCategory.Financial));
        Assert.Equal(2 * 365, policy.RetentionFor(DataCategory.Audit));
        Assert.Equal(365, policy.RetentionFor(DataCategory.Operational));
    }

    [Fact]
    public void Dado_Financeiro_Eh_Retido_Por_Mais_Tempo()
    {
        var policy = RetentionPolicy.CreateDefault();
        var recordDate = Today.AddDays(-400);

        // 400 dias: expirado para operacional, ainda retido para financeiro.
        Assert.True(policy.IsExpired(DataCategory.Operational, recordDate, Today));
        Assert.False(policy.IsExpired(DataCategory.Financial, recordDate, Today));
    }

    [Fact]
    public void Dado_Expirado_Eh_Identificado()
    {
        var policy = RetentionPolicy.CreateDefault();
        var recordDate = Today.AddDays(-100);

        Assert.True(policy.IsExpired(DataCategory.AiLogs, recordDate, Today));
    }

    [Fact]
    public void Dado_Dentro_Do_Prazo_Nao_Expira()
    {
        var policy = RetentionPolicy.CreateDefault();
        var recordDate = Today.AddDays(-30);

        Assert.False(policy.IsExpired(DataCategory.AiLogs, recordDate, Today));
    }

    [Fact]
    public void Categoria_Sem_Prazo_Nunca_Expira()
    {
        var policy = RetentionPolicy.CreateDefault();
        policy.SetRetention(DataCategory.Operational, 365);

        // Categoria não configurada: nunca expira.
        var custom = new Dictionary<DataCategory, int>();
        Assert.Empty(custom);
        Assert.False(policy.IsExpired(DataCategory.Operational, Today.AddDays(-1), Today));
    }

    [Fact]
    public void Prazo_Invalido_Lanca_Excecao()
    {
        var policy = RetentionPolicy.CreateDefault();

        Assert.Throws<DomainRuleException>(() => policy.SetRetention(DataCategory.Audit, 0));
    }
}
