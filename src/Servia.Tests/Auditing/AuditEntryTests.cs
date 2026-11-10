using Servia.Domain.Auditing;
using Servia.Domain.Common;

namespace Servia.Tests.Auditing;

/// <summary>
/// F1.8 — auditoria de ações sensíveis.
/// Verifica registro, imutabilidade e separação entre ações de tenant e
/// de plataforma.
/// </summary>
public class AuditEntryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid EntityId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static AuditEntry NewEntry() =>
        AuditEntry.Record(
            "Appointment.Cancelled",
            "Appointment",
            EntityId,
            Now,
            tenantId: TenantId,
            actorId: ActorId,
            changes: "{\"status\":\"Cancelled\"}",
            ipAddress: "203.0.113.10");

    // ---------- Registro ----------

    [Fact]
    public void Registrar_Acao_Preenche_Todos_Os_Campos()
    {
        var entry = NewEntry();

        Assert.Equal("Appointment.Cancelled", entry.Action);
        Assert.Equal("Appointment", entry.EntityType);
        Assert.Equal(EntityId, entry.EntityId);
        Assert.Equal(TenantId, entry.TenantId);
        Assert.Equal(ActorId, entry.ActorId);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("203.0.113.10", entry.IpAddress);
        Assert.Contains("Cancelled", entry.Changes);
    }

    [Fact]
    public void Registrar_Acao_Sem_Descricao_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AuditEntry.Record("  ", "Appointment", EntityId, Now));
    }

    [Fact]
    public void Registrar_Acao_Sem_Tipo_De_Entidade_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AuditEntry.Record("Appointment.Cancelled", "", EntityId, Now));
    }

    [Fact]
    public void Registrar_Acao_Sem_Entidade_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AuditEntry.Record("Appointment.Cancelled", "Appointment", Guid.Empty, Now));
    }

    // ---------- Imutabilidade ----------

    [Fact]
    public void Entrada_De_Auditoria_Nao_Expoe_Metodos_De_Alteracao()
    {
        var methods = typeof(AuditEntry)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        // O único método público é o de registro (estático) e os herdados.
        Assert.DoesNotContain("Update", methods);
        Assert.DoesNotContain("Delete", methods);
        Assert.DoesNotContain("Remove", methods);
        Assert.DoesNotContain("Change", methods);
    }

    [Fact]
    public void Propriedades_De_Auditoria_Nao_Tem_Setter_Publico()
    {
        var properties = typeof(AuditEntry).GetProperties();

        Assert.All(properties, property =>
            Assert.False(property.CanWrite && property.SetMethod?.IsPublic == true,
                $"A propriedade {property.Name} não deveria ter setter público."));
    }

    // ---------- Tenant vs plataforma ----------

    [Fact]
    public void Acao_De_Tenant_Registra_O_Estabelecimento()
    {
        var entry = NewEntry();

        Assert.Equal(TenantId, entry.TenantId);
        Assert.False(entry.IsPlatformAction);
    }

    [Fact]
    public void Acao_Da_Plataforma_Nao_Registra_Tenant()
    {
        var entry = AuditEntry.Record(
            "Tenant.Suspended", "Tenant", EntityId, Now, actorId: ActorId);

        Assert.Null(entry.TenantId);
        Assert.True(entry.IsPlatformAction);
    }

    [Fact]
    public void Acao_Do_Sistema_Pode_Nao_Ter_Autor()
    {
        var entry = AuditEntry.Record("Subscription.Renewed", "Subscription", EntityId, Now, TenantId);

        Assert.Null(entry.ActorId);
    }

    // ---------- Identidade ----------

    [Fact]
    public void Cada_Registro_Recebe_Identificador_Proprio()
    {
        var first = NewEntry();
        var second = NewEntry();

        Assert.NotEqual(first.Id, second.Id);
    }
}
