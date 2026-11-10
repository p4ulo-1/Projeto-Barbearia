using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Team;

namespace Servia.Tests.Team;

/// <summary>
/// Invariantes do profissional e a composição com a jornada de trabalho.
/// Critério B05: composição comprovada na implementação.
/// </summary>
public class ProfessionalTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static WorkSchedule Schedule() =>
        WorkSchedule.Create(Weekdays, new TimeOnly(9, 0), new TimeOnly(18, 0));

    private static Professional NewProfessional() =>
        Professional.Create(TenantId, "Ana Souza", Schedule(), title: "Cabeleireira");

    [Fact]
    public void Criar_Profissional_Valido_Inicia_Ativo()
    {
        var professional = NewProfessional();

        Assert.Equal("Ana Souza", professional.Name);
        Assert.Equal("Cabeleireira", professional.Title);
        Assert.True(professional.IsActive);
        Assert.NotEqual(Guid.Empty, professional.Id);
    }

    [Fact]
    public void Criar_Profissional_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Professional.Create(Guid.Empty, "Ana", Schedule()));
    }

    [Fact]
    public void Criar_Profissional_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Professional.Create(TenantId, "  ", Schedule()));
    }

    [Fact]
    public void Criar_Profissional_Sem_Jornada_Lanca_Excecao()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Professional.Create(TenantId, "Ana", null!));
    }

    // ---------- Composição: Professional ◆—— WorkSchedule ----------

    [Fact]
    public void Profissional_Possui_Jornada_Como_Parte_Do_Seu_Estado()
    {
        var professional = NewProfessional();

        Assert.NotNull(professional.Schedule);
        Assert.Equal(new TimeOnly(9, 0), professional.Schedule.StartTime);
        Assert.Equal(new TimeOnly(18, 0), professional.Schedule.EndTime);
    }

    [Fact]
    public void Atualizar_Jornada_Substitui_A_Jornada_Anterior()
    {
        var professional = NewProfessional();
        var newSchedule = WorkSchedule.Create(Weekdays, new TimeOnly(8, 0), new TimeOnly(17, 0));

        professional.UpdateSchedule(newSchedule);

        Assert.Equal(new TimeOnly(8, 0), professional.Schedule.StartTime);
        Assert.Equal(new TimeOnly(17, 0), professional.Schedule.EndTime);
    }

    [Fact]
    public void Atualizar_Jornada_Nula_Lanca_Excecao()
    {
        var professional = NewProfessional();

        Assert.Throws<ArgumentNullException>(() => professional.UpdateSchedule(null!));
    }

    [Fact]
    public void Jornadas_De_Profissionais_Diferentes_Sao_Independentes()
    {
        var first = NewProfessional();
        var second = NewProfessional();

        first.UpdateSchedule(WorkSchedule.Create(Weekdays, new TimeOnly(7, 0), new TimeOnly(12, 0)));

        // A jornada do segundo não foi afetada: não há compartilhamento.
        Assert.Equal(new TimeOnly(9, 0), second.Schedule.StartTime);
        Assert.Equal(new TimeOnly(7, 0), first.Schedule.StartTime);
    }

    // ---------- Demais comportamentos ----------

    [Fact]
    public void Atualizar_Perfil_Sem_Nome_Lanca_Excecao()
    {
        var professional = NewProfessional();

        Assert.Throws<DomainRuleException>(() =>
            professional.UpdateProfile("", null, null, null));
    }

    [Fact]
    public void Vincular_Usuario_Registra_Identificador()
    {
        var professional = NewProfessional();
        var userId = Guid.NewGuid();

        professional.LinkUser(userId);

        Assert.Equal(userId, professional.UserId);
    }

    [Fact]
    public void Vincular_Usuario_Vazio_Lanca_Excecao()
    {
        var professional = NewProfessional();

        Assert.Throws<DomainRuleException>(() => professional.LinkUser(Guid.Empty));
    }

    [Fact]
    public void Desativar_E_Reativar_Alteram_Estado()
    {
        var professional = NewProfessional();

        professional.Deactivate();
        Assert.False(professional.IsActive);

        professional.Activate();
        Assert.True(professional.IsActive);
    }
}
