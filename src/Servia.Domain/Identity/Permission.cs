namespace Servia.Domain.Identity;

/// <summary>
/// Permissões granulares do sistema.
///
/// Cada permissão representa uma capacidade concreta. Os papéis são
/// combinações dessas permissões, definidas em <see cref="RolePermissions"/>.
/// A verificação acontece <b>no servidor</b>, nunca apenas no cliente.
/// </summary>
public enum Permission
{
    // ---------- Estabelecimento ----------
    ViewTenantSettings = 1,
    ManageTenantSettings = 2,
    ManageTeam = 3,
    ManageBilling = 4,

    // ---------- Catálogo ----------
    ViewServices = 10,
    ManageServices = 11,

    // ---------- Agenda ----------
    ViewOwnAgenda = 20,
    ViewTeamAgenda = 21,
    ManageAppointments = 22,
    CancelAnyAppointment = 23,

    // ---------- Clientes ----------
    ViewCustomers = 30,
    ManageCustomers = 31,

    // ---------- Financeiro ----------
    ViewFinancialReports = 40,
    ManageFinancialEntries = 41,
    ViewOwnCommissions = 42,

    // ---------- Plataforma ----------
    ManagePlatformTenants = 90,
    ManagePlatformPlans = 91
}
