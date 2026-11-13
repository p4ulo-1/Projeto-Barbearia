using Servia.Domain.Common;

namespace Servia.Domain.Identity;

/// <summary>
/// Mapeamento de papéis para permissões.
///
/// Centraliza a autorização em um único ponto: adicionar uma permissão a um
/// papel é uma alteração aqui, não em cada controller.
///
/// <b>Separação de privilégios (F1.13):</b> <see cref="Role.PlatformAdmin"/>
/// tem apenas permissões de plataforma e <b>nenhuma</b> permissão de tenant.
/// Os demais papéis têm apenas permissões de tenant e nenhuma de plataforma.
/// Isso impede que um administrador de estabelecimento execute operações da
/// plataforma, e vice-versa.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlySet<Permission> PlatformAdminPermissions = new HashSet<Permission>
    {
        Permission.ManagePlatformTenants,
        Permission.ManagePlatformPlans
    };

    private static readonly IReadOnlySet<Permission> OwnerPermissions = new HashSet<Permission>
    {
        Permission.ViewTenantSettings,
        Permission.ManageTenantSettings,
        Permission.ManageTeam,
        Permission.ManageBilling,
        Permission.ViewServices,
        Permission.ManageServices,
        Permission.ViewTeamAgenda,
        Permission.ManageAppointments,
        Permission.CancelAnyAppointment,
        Permission.ViewCustomers,
        Permission.ManageCustomers,
        Permission.ViewFinancialReports,
        Permission.ManageFinancialEntries
    };

    /// <summary>
    /// Administrador do estabelecimento: funcionário indicado pelo Owner.
    ///
    /// Tem acesso amplo às operações e aos dados do estabelecimento —
    /// equipe, serviços, clientes, agenda, configurações e financeiro
    /// interno. <b>Não</b> tem <see cref="Permission.ManageBilling"/>: a
    /// relação comercial com a plataforma (contratar/alterar/cancelar plano)
    /// é exclusiva do <see cref="Role.Owner"/>.
    /// </summary>
    private static readonly IReadOnlySet<Permission> AdministratorPermissions = new HashSet<Permission>
    {
        Permission.ViewTenantSettings,
        Permission.ManageTenantSettings,
        Permission.ManageTeam,
        Permission.ViewServices,
        Permission.ManageServices,
        Permission.ViewTeamAgenda,
        Permission.ManageAppointments,
        Permission.CancelAnyAppointment,
        Permission.ViewCustomers,
        Permission.ManageCustomers,
        Permission.ViewFinancialReports,
        Permission.ManageFinancialEntries
    };

    private static readonly IReadOnlySet<Permission> ProfessionalPermissions = new HashSet<Permission>
    {
        Permission.ViewServices,
        Permission.ViewOwnAgenda,
        Permission.ManageAppointments,
        Permission.ViewCustomers,
        Permission.ViewOwnCommissions
    };

    /// <summary>
    /// Recepcionista: permissões operacionais superiores às do profissional
    /// comum, com foco na administração da agenda de toda a equipe.
    ///
    /// Vê os relatórios financeiros (visão operacional do caixa), mas não
    /// lança pagamentos nem transações — a escrita financeira permanece com
    /// o administrador/proprietário.
    /// </summary>
    private static readonly IReadOnlySet<Permission> ReceptionistPermissions = new HashSet<Permission>
    {
        Permission.ViewServices,
        Permission.ViewTeamAgenda,
        Permission.ManageAppointments,
        Permission.CancelAnyAppointment,
        Permission.ViewCustomers,
        Permission.ManageCustomers,
        Permission.ViewFinancialReports
    };

    private static readonly IReadOnlySet<Permission> CustomerPermissions = new HashSet<Permission>
    {
        Permission.ViewServices
    };

    private static readonly IReadOnlyDictionary<Role, IReadOnlySet<Permission>> Map =
        new Dictionary<Role, IReadOnlySet<Permission>>
        {
            [Role.PlatformAdmin] = PlatformAdminPermissions,
            [Role.Owner] = OwnerPermissions,
            [Role.Administrator] = AdministratorPermissions,
            [Role.Professional] = ProfessionalPermissions,
            [Role.Receptionist] = ReceptionistPermissions,
            [Role.Customer] = CustomerPermissions
        };

    /// <summary>Retorna as permissões de um papel.</summary>
    public static IReadOnlySet<Permission> For(Role role) =>
        Map.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<Permission>();

    /// <summary>Indica se o papel possui a permissão informada.</summary>
    public static bool Has(Role role, Permission permission) => For(role).Contains(permission);

    /// <summary>
    /// Indica se o papel pertence à plataforma, e não a um estabelecimento.
    /// </summary>
    public static bool IsPlatformRole(Role role) => role == Role.PlatformAdmin;

    /// <summary>
    /// Indica se o papel pertence a um estabelecimento.
    /// </summary>
    public static bool IsTenantRole(Role role) => role != Role.PlatformAdmin;

    /// <summary>
    /// Permissões exclusivas do <see cref="Role.Owner"/> (titularidade da
    /// conta e relação comercial com a plataforma). Não podem ser concedidas
    /// a papéis personalizados nem a outros papéis predefinidos.
    /// </summary>
    public static IReadOnlySet<Permission> OwnerExclusivePermissions { get; } =
        new HashSet<Permission> { Permission.ManageBilling };

    /// <summary>
    /// Permissões exclusivas da plataforma. Nunca pertencem a um tenant.
    /// </summary>
    public static IReadOnlySet<Permission> PlatformPermissions { get; } =
        new HashSet<Permission>
        {
            Permission.ManagePlatformTenants,
            Permission.ManagePlatformPlans
        };

    /// <summary>
    /// Permissões que podem ser concedidas a um papel personalizado: todas
    /// as de tenant, exceto as exclusivas do Owner e as de plataforma.
    /// </summary>
    public static IReadOnlySet<Permission> AssignableToCustomRole { get; } =
        Enum.GetValues<Permission>()
            .Where(permission => !OwnerExclusivePermissions.Contains(permission)
                && !PlatformPermissions.Contains(permission))
            .ToHashSet();

    /// <summary>
    /// Garante que a permissão pode ser concedida a um papel personalizado.
    /// </summary>
    public static void EnsureAssignableToCustomRole(Permission permission)
    {
        if (OwnerExclusivePermissions.Contains(permission))
        {
            throw new DomainRuleException(
                $"A permissão {permission} é exclusiva do proprietário e não pode ser concedida a um papel personalizado.");
        }

        if (PlatformPermissions.Contains(permission))
        {
            throw new DomainRuleException(
                $"A permissão {permission} é exclusiva da plataforma e não pode ser concedida a um papel personalizado.");
        }
    }

    /// <summary>
    /// Garante que o ator não concede permissões que ele próprio não possui.
    ///
    /// Impede escalonamento de privilégios: quem administra a equipe
    /// (<see cref="Permission.ManageTeam"/>) não pode criar ou atribuir um
    /// papel com permissões além das suas.
    ///
    /// O <see cref="Role.Owner"/> é a autoridade máxima do tenant: pode
    /// conceder qualquer permissão <b>atribuível</b> a papel personalizado,
    /// inclusive as de escopo próprio (ViewOwnAgenda/ViewOwnCommissions), que
    /// ele não exerce diretamente. As exclusivas do Owner e da plataforma
    /// continuam bloqueadas por <see cref="EnsureAssignableToCustomRole"/>.
    /// </summary>
    public static void EnsureCanGrant(
        IReadOnlySet<Permission> actorPermissions,
        IEnumerable<Permission> granted)
    {
        ArgumentNullException.ThrowIfNull(actorPermissions);
        ArgumentNullException.ThrowIfNull(granted);

        var isOwner = actorPermissions.SetEquals(OwnerPermissions);

        foreach (var permission in granted)
        {
            if (isOwner)
            {
                EnsureAssignableToCustomRole(permission);
                continue;
            }

            if (!actorPermissions.Contains(permission))
            {
                throw new DomainRuleException(
                    $"Você não pode conceder a permissão {permission}, que não possui.");
            }
        }
    }
}
