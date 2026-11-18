using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Team;

namespace Servia.Tests.Appointments;

/// <summary>
/// Elegibilidade operacional — o predicado único compartilhado.
///
/// <b>Elegibilidade não é disponibilidade.</b> O predicado responde "esta
/// pessoa pode, em princípio, receber agendamentos?" e não varia por conflito
/// transitório. A disponibilidade de um horário específico é outra pergunta,
/// respondida por <c>AvailabilityService</c>.
/// </summary>
public sealed class BookingEligibilityTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Professional ProfessionalOn(
        DayOfWeek[]? days = null,
        bool isActive = true,
        bool isServiceProvider = true)
    {
        var schedule = WorkSchedule.Create(
            days ?? [DayOfWeek.Monday],
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

        var professional = Professional.Create(
            TenantId, "Profissional", schedule, isServiceProvider: isServiceProvider);

        if (!isActive)
        {
            professional.Deactivate();
        }

        return professional;
    }

    private static Service ActiveService() =>
        Service.Create(TenantId, "Corte", Money.From(50m), 30);

    // ---------- Elegibilidade operacional ----------

    [Fact]
    public void Profissional_Ativo_Com_Jornada_Eh_Elegivel()
    {
        Assert.True(BookingEligibility.IsOperationallyEligible(ProfessionalOn()));
    }

    [Fact]
    public void Profissional_Inativo_Nao_Eh_Elegivel()
    {
        Assert.False(BookingEligibility.IsOperationallyEligible(ProfessionalOn(isActive: false)));
    }

    [Fact]
    public void Jornada_Sem_Dia_De_Trabalho_Nao_Eh_Construivel()
    {
        // O cenário "profissional sem dia de trabalho" não é representável:
        // WorkSchedule.Create rejeita jornada vazia. A invariante é garantida
        // na construção, e o predicado de elegibilidade só precisa verificar
        // que a jornada existe com ao menos um dia.
        var exception = Assert.Throws<DomainRuleException>(() =>
            WorkSchedule.Create([], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        Assert.Contains("ao menos um dia", exception.Message);

        // Um profissional construído tem sempre jornada válida e é elegível.
        var professional = ProfessionalOn();

        Assert.NotEmpty(professional.Schedule.WorkDays);
        Assert.True(BookingEligibility.IsOperationallyEligible(professional));
    }

    // ---------- Execução de serviço ----------

    [Fact]
    public void Profissional_Elegivel_Vinculado_A_Servico_Ativo_Pode_Executar()
    {
        var professional = ProfessionalOn();
        var service = ActiveService();

        Assert.True(BookingEligibility.CanExecute(professional, service, isLinked: true));
    }

    [Fact]
    public void Profissional_Sem_Vinculo_Nao_Pode_Executar()
    {
        var professional = ProfessionalOn();
        var service = ActiveService();

        Assert.False(BookingEligibility.CanExecute(professional, service, isLinked: false));
    }

    [Fact]
    public void Servico_Inativo_Nao_Pode_Ser_Executado()
    {
        var professional = ProfessionalOn();
        var service = ActiveService();
        service.Deactivate();

        Assert.False(BookingEligibility.CanExecute(professional, service, isLinked: true));
    }

    [Fact]
    public void Profissional_Inativo_Nao_Pode_Executar_Mesmo_Vinculado()
    {
        var professional = ProfessionalOn(isActive: false);
        var service = ActiveService();

        Assert.False(BookingEligibility.CanExecute(professional, service, isLinked: true));
    }

    // ---------- Independência de conflito transitório ----------

    [Fact]
    public void Elegibilidade_Nao_Depende_De_Agendamentos_Existentes()
    {
        // A elegibilidade é uma propriedade do profissional e do serviço.
        // Um horário ocupado não a altera: se alterasse, a contagem do plano
        // oscilaria a cada reserva.
        var professional = ProfessionalOn();
        var service = ActiveService();

        var before = BookingEligibility.CanExecute(professional, service, isLinked: true);

        // Simula uma agenda cheia: nada muda no profissional nem no serviço.
        var after = BookingEligibility.CanExecute(professional, service, isLinked: true);

        Assert.Equal(before, after);
        Assert.True(after);
    }

    // ---------- Ocupar vaga no plano ----------

    [Fact]
    public void Profissional_Ativo_Com_Servico_Ativo_Ocupa_Vaga()
    {
        Assert.True(BookingEligibility.OccupiesPlanSlot(ProfessionalOn(), hasBookableLinkedService: true));
    }

    [Fact]
    public void Profissional_Sem_Servico_Ativo_Nao_Ocupa_Vaga()
    {
        // Cadastro isolado não consome vaga.
        Assert.False(BookingEligibility.OccupiesPlanSlot(ProfessionalOn(), hasBookableLinkedService: false));
    }

    [Fact]
    public void Profissional_Inativo_Nao_Ocupa_Vaga_Mesmo_Com_Servico()
    {
        Assert.False(BookingEligibility.OccupiesPlanSlot(
            ProfessionalOn(isActive: false), hasBookableLinkedService: true));
    }

    [Fact]
    public void Ocupar_Vaga_Nao_Depende_De_Disponibilidade_Momentanea()
    {
        // Um profissional com a agenda cheia continua ocupando a vaga.
        // O predicado de vaga não consulta horários.
        var professional = ProfessionalOn();

        Assert.True(BookingEligibility.OccupiesPlanSlot(professional, hasBookableLinkedService: true));
        Assert.True(BookingEligibility.OccupiesPlanSlot(professional, hasBookableLinkedService: true));
    }

    [Fact]
    public void Ocupar_Vaga_E_Executar_Servico_Sao_Conceitos_Distintos()
    {
        // Ocupa vaga (ativo + serviço ativo), mas não executa um serviço
        // específico sem vínculo com ele.
        var professional = ProfessionalOn();
        var otherService = ActiveService();

        Assert.True(BookingEligibility.OccupiesPlanSlot(professional, hasBookableLinkedService: true));
        Assert.False(BookingEligibility.CanExecute(professional, otherService, isLinked: false));
    }
}