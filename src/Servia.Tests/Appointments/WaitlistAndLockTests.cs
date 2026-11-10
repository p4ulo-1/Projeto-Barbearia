using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Tests.Appointments;

/// <summary>
/// F2.8 — fila de espera com oferta de vaga liberada.
/// F2.11 — controle de concorrência na reserva.
/// </summary>
public class WaitlistAndLockTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 10);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static WaitlistEntry NewEntry() =>
        WaitlistEntry.Create(TenantId, CustomerId, ServiceId, ProfessionalId, Date, Now);

    // ---------- F2.8: fila de espera ----------

    [Fact]
    public void Criar_Entrada_Na_Fila_Inicia_Em_Espera()
    {
        var entry = NewEntry();

        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        Assert.True(entry.IsWaiting);
        Assert.Equal(Date, entry.PreferredDate);
    }

    [Fact]
    public void Criar_Entrada_Sem_Cliente_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            WaitlistEntry.Create(TenantId, Guid.Empty, ServiceId, ProfessionalId, Date, Now));
    }

    [Fact]
    public void Criar_Entrada_Sem_Data_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            WaitlistEntry.Create(TenantId, CustomerId, ServiceId, ProfessionalId, default, Now));
    }

    [Fact]
    public void Ofertar_Vaga_Muda_Status_Para_Ofertada()
    {
        var entry = NewEntry();

        entry.Offer(new TimeOnly(14, 0), Now);

        Assert.Equal(WaitlistStatus.Offered, entry.Status);
        Assert.Equal(new TimeOnly(14, 0), entry.OfferedTime);
        Assert.Equal(Now.AddMinutes(WaitlistEntry.DefaultOfferWindowMinutes), entry.OfferExpiresAt);
    }

    [Fact]
    public void Aceitar_Oferta_Converte_Em_Aceita()
    {
        var entry = NewEntry();
        entry.Offer(new TimeOnly(14, 0), Now);

        entry.Accept(Now.AddMinutes(10));

        Assert.Equal(WaitlistStatus.Accepted, entry.Status);
    }

    [Fact]
    public void Aceitar_Oferta_Expirada_Lanca_Excecao()
    {
        var entry = NewEntry();
        entry.Offer(new TimeOnly(14, 0), Now);

        Assert.Throws<DomainRuleException>(() =>
            entry.Accept(Now.AddMinutes(WaitlistEntry.DefaultOfferWindowMinutes)));
    }

    [Fact]
    public void Aceitar_Sem_Oferta_Lanca_Excecao()
    {
        var entry = NewEntry();

        Assert.Throws<DomainRuleException>(() => entry.Accept(Now));
    }

    [Fact]
    public void Expirar_Oferta_Libera_A_Vaga()
    {
        var entry = NewEntry();
        entry.Offer(new TimeOnly(14, 0), Now);

        entry.ExpireOffer();

        Assert.Equal(WaitlistStatus.Expired, entry.Status);
        Assert.Null(entry.OfferedTime);
    }

    [Fact]
    public void Retirar_Entrada_Da_Fila()
    {
        var entry = NewEntry();

        entry.Withdraw();

        Assert.Equal(WaitlistStatus.Withdrawn, entry.Status);
    }

    [Fact]
    public void Entrada_Aceita_Nao_Pode_Ser_Retirada()
    {
        var entry = NewEntry();
        entry.Offer(new TimeOnly(14, 0), Now);
        entry.Accept(Now.AddMinutes(5));

        Assert.Throws<DomainRuleException>(() => entry.Withdraw());
    }

    // ---------- F2.11: concorrencia ----------

    [Fact]
    public void Adquirir_Trava_Define_Expiracao()
    {
        var scheduleLock = ScheduleLock.Acquire(
            TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 30, Now);

        Assert.True(scheduleLock.IsActive(Now));
        Assert.Equal(Now.AddMinutes(ScheduleLock.DefaultLifetimeMinutes), scheduleLock.ExpiresAt);
        Assert.Equal(new TimeOnly(10, 30), scheduleLock.EndTime);
    }

    [Fact]
    public void Trava_Expira_Apos_O_Prazo()
    {
        var scheduleLock = ScheduleLock.Acquire(
            TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 30, Now);

        Assert.False(scheduleLock.IsActive(Now.AddMinutes(ScheduleLock.DefaultLifetimeMinutes)));
    }

    [Fact]
    public void Liberar_Trava_Permite_Nova_Reserva()
    {
        var scheduleLock = ScheduleLock.Acquire(
            TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 30, Now);

        scheduleLock.Release();

        Assert.False(scheduleLock.IsActive(Now));
    }

    [Fact]
    public void Vincular_Trava_Ao_Agendamento()
    {
        var scheduleLock = ScheduleLock.Acquire(
            TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 30, Now);
        var appointmentId = Guid.NewGuid();

        scheduleLock.AttachTo(appointmentId);

        Assert.Equal(appointmentId, scheduleLock.AppointmentId);
    }

    [Fact]
    public void Adquirir_Trava_Sem_Profissional_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ScheduleLock.Acquire(TenantId, Guid.Empty, Date, new TimeOnly(10, 0), 30, Now));
    }

    [Fact]
    public void Adquirir_Trava_Com_Duracao_Invalida_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ScheduleLock.Acquire(TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 0, Now));
    }

    [Fact]
    public void Duas_Travas_Para_O_Mesmo_Horario_Sao_Identificaveis()
    {
        // O banco rejeita a segunda pelo índice único parcial. Aqui
        // verificamos que ambas carregam a mesma chave de negócio.
        var first = ScheduleLock.Acquire(TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 30, Now);
        var second = ScheduleLock.Acquire(TenantId, ProfessionalId, Date, new TimeOnly(10, 0), 30, Now);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.TenantId, second.TenantId);
        Assert.Equal(first.ProfessionalId, second.ProfessionalId);
        Assert.Equal(first.Date, second.Date);
        Assert.Equal(first.StartTime, second.StartTime);
    }
}
