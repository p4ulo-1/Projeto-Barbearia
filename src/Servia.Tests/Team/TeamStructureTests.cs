using Servia.Domain.Common;
using Servia.Domain.Team;

namespace Servia.Tests.Team;

/// <summary>
/// F2.5 — vínculo profissional ↔ serviço.
/// F2.6 — bloqueios e folgas de agenda.
/// </summary>
public class TeamStructureTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // ---------- F2.5: vinculo ----------

    [Fact]
    public void Criar_Vinculo_Profissional_Servico()
    {
        var serviceId = Guid.NewGuid();

        var link = ProfessionalService.Create(TenantId, ProfessionalId, serviceId);

        Assert.Equal(TenantId, link.TenantId);
        Assert.Equal(ProfessionalId, link.ProfessionalId);
        Assert.Equal(serviceId, link.ServiceId);
    }

    [Fact]
    public void Criar_Vinculo_Sem_Profissional_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ProfessionalService.Create(TenantId, Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Criar_Vinculo_Sem_Servico_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ProfessionalService.Create(TenantId, ProfessionalId, Guid.Empty));
    }

    // ---------- F2.6: bloqueios ----------

    [Fact]
    public void Criar_Bloqueio_Valido()
    {
        var block = ScheduleBlock.Create(
            TenantId, ProfessionalId, Now, Now.AddHours(1), "Almoço");

        Assert.Equal(Now, block.Start);
        Assert.Equal(Now.AddHours(1), block.End);
        Assert.Equal("Almoço", block.Reason);
    }

    [Fact]
    public void Criar_Bloqueio_Com_Fim_Antes_Do_Inicio_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ScheduleBlock.Create(TenantId, ProfessionalId, Now.AddHours(1), Now));
    }

    [Fact]
    public void Criar_Bloqueio_Com_Duracao_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ScheduleBlock.Create(TenantId, ProfessionalId, Now, Now));
    }

    [Fact]
    public void Bloqueio_Detecta_Sobreposicao()
    {
        var block = ScheduleBlock.Create(TenantId, ProfessionalId, Now, Now.AddHours(2));

        Assert.True(block.Overlaps(Now.AddMinutes(30), Now.AddHours(1)));
    }

    [Fact]
    public void Bloqueio_Nao_Detecta_Intervalo_Adjacente()
    {
        var block = ScheduleBlock.Create(TenantId, ProfessionalId, Now, Now.AddHours(1));

        Assert.False(block.Overlaps(Now.AddHours(1), Now.AddHours(2)));
    }

    [Fact]
    public void Bloqueio_Nao_Detecta_Intervalo_Disjunto()
    {
        var block = ScheduleBlock.Create(TenantId, ProfessionalId, Now, Now.AddHours(1));

        Assert.False(block.Overlaps(Now.AddHours(3), Now.AddHours(4)));
    }

    [Fact]
    public void Bloqueio_Detecta_Intervalo_Que_Contem_O_Bloqueio()
    {
        var block = ScheduleBlock.Create(TenantId, ProfessionalId, Now.AddHours(1), Now.AddHours(2));

        Assert.True(block.Overlaps(Now, Now.AddHours(3)));
    }
}
