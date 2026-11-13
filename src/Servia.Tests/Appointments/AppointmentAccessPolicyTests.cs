using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Identity;

namespace Servia.Tests.Appointments;

/// <summary>
/// Seção 3 do pedido de ajustes: permissões sobre compromissos.
///
/// Profissional comum: apenas a própria agenda.
/// Recepcionista e administrador: qualquer profissional do estabelecimento.
/// </summary>
public class AppointmentAccessPolicyTests
{
    private static readonly Guid OwnProfessional = Guid.NewGuid();
    private static readonly Guid OtherProfessional = Guid.NewGuid();

    [Theory]
    [InlineData(Role.Owner)]
    [InlineData(Role.Administrator)]
    [InlineData(Role.Receptionist)]
    public void Gestores_Administram_Qualquer_Profissional(Role role)
    {
        Assert.True(AppointmentAccessPolicy.CanManageAnyAppointment(role));

        // Não lança para o próprio nem para outro profissional.
        AppointmentAccessPolicy.EnsureCanManage(role, null, OwnProfessional);
        AppointmentAccessPolicy.EnsureCanManage(role, null, OtherProfessional);
    }

    [Fact]
    public void Profissional_Administra_Apenas_A_Propria_Agenda()
    {
        Assert.True(AppointmentAccessPolicy.CanManageOnlyOwnAppointments(Role.Professional));

        // Própria agenda: permitido.
        AppointmentAccessPolicy.EnsureCanManage(Role.Professional, OwnProfessional, OwnProfessional);

        // Agenda de terceiro: bloqueado.
        Assert.Throws<DomainRuleException>(() =>
            AppointmentAccessPolicy.EnsureCanManage(Role.Professional, OwnProfessional, OtherProfessional));
    }

    [Fact]
    public void Profissional_Sem_Vinculo_Nao_Administra_Nenhuma_Agenda()
    {
        Assert.Throws<DomainRuleException>(() =>
            AppointmentAccessPolicy.EnsureCanManage(Role.Professional, null, OtherProfessional));
    }

    [Fact]
    public void Cliente_Nao_Administra_Compromissos()
    {
        Assert.False(AppointmentAccessPolicy.CanManageAnyAppointment(Role.Customer));
        Assert.False(AppointmentAccessPolicy.CanManageOnlyOwnAppointments(Role.Customer));

        Assert.Throws<DomainRuleException>(() =>
            AppointmentAccessPolicy.EnsureCanManage(Role.Customer, null, OtherProfessional));
    }

    [Fact]
    public void Visualizacao_Segue_A_Mesma_Regra()
    {
        Assert.True(AppointmentAccessPolicy.CanView(Role.Administrator, null, OtherProfessional));
        Assert.True(AppointmentAccessPolicy.CanView(Role.Receptionist, null, OtherProfessional));
        Assert.True(AppointmentAccessPolicy.CanView(Role.Professional, OwnProfessional, OwnProfessional));
        Assert.False(AppointmentAccessPolicy.CanView(Role.Professional, OwnProfessional, OtherProfessional));
        Assert.False(AppointmentAccessPolicy.CanView(Role.Customer, null, OtherProfessional));
    }
}
