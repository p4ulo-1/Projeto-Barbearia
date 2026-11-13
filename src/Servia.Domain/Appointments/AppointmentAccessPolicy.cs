using Servia.Domain.Common;
using Servia.Domain.Identity;

namespace Servia.Domain.Appointments;

/// <summary>
/// Regra de quem pode administrar um compromisso (criar, remarcar, concluir,
/// cancelar).
///
/// <b>Profissional comum:</b> apenas os próprios compromissos — os que têm
/// ele como profissional responsável.
///
/// <b>Recepcionista e administrador:</b> os compromissos de qualquer
/// profissional do estabelecimento.
///
/// A verificação é feita no servidor, a partir do papel autenticado e do
/// profissional vinculado ao usuário. Restringir apenas a interface não
/// impede chamadas diretas à API.
/// </summary>
public static class AppointmentAccessPolicy
{
    /// <summary>
    /// Indica se o papel administra compromissos de qualquer profissional.
    /// </summary>
    public static bool CanManageAnyAppointment(Role role) =>
        role is Role.Owner or Role.Administrator or Role.Receptionist;

    /// <summary>
    /// Indica se o papel administra apenas os próprios compromissos.
    /// </summary>
    public static bool CanManageOnlyOwnAppointments(Role role) =>
        role == Role.Professional;

    /// <summary>
    /// Garante que o usuário pode administrar o compromisso do profissional
    /// informado. Lança <see cref="DomainRuleException"/> quando não pode.
    /// </summary>
    /// <param name="role">Papel do usuário autenticado.</param>
    /// <param name="userProfessionalId">
    /// Profissional vinculado ao usuário autenticado, quando houver.
    /// </param>
    /// <param name="targetProfessionalId">
    /// Profissional responsável pelo compromisso alvo.
    /// </param>
    public static void EnsureCanManage(
        Role role,
        Guid? userProfessionalId,
        Guid targetProfessionalId)
    {
        if (CanManageAnyAppointment(role))
        {
            return;
        }

        if (CanManageOnlyOwnAppointments(role))
        {
            if (userProfessionalId.HasValue && userProfessionalId.Value == targetProfessionalId)
            {
                return;
            }

            throw new DomainRuleException(
                "Você só pode administrar os compromissos da sua própria agenda.");
        }

        throw new DomainRuleException(
            "Seu perfil não permite administrar compromissos.");
    }

    /// <summary>
    /// Indica se o usuário pode ver o compromisso do profissional informado.
    /// Mesma regra de administração, aplicada à leitura da agenda.
    /// </summary>
    public static bool CanView(Role role, Guid? userProfessionalId, Guid targetProfessionalId) =>
        CanManageAnyAppointment(role)
        || (CanManageOnlyOwnAppointments(role)
            && userProfessionalId.HasValue
            && userProfessionalId.Value == targetProfessionalId);
}
