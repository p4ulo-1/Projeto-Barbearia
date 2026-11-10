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
        Permission.ViewFinancialReports
    };

    private static readonly IReadOnlySet<Permission> ProfessionalPermissions = new HashSet<Permission>
    {
        Permission.ViewServices,
        Permission.ViewOwnAgenda,
        Permission.ManageAppointments,
        Permission.ViewCustomers,
        Permission.ViewOwnCommissions
    };

    private static readonly IReadOnlySet<Permission> ReceptionistPermissions = new HashSet<Permission>
    {
        Permission.ViewServices,
        Permission.ViewTeamAgenda,
        Permission.ManageAppointments,
        Permission.ViewCustomers,
        Permission.ManageCustomers
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
}
