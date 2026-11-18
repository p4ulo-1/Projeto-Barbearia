import type { UserDto, UserRole } from "@/lib/api/types";

const codes = {
  ViewTenantSettings: 1,
  ManageTenantSettings: 2,
  ManageTeam: 3,
  ManageBilling: 4,
  ViewServices: 10,
  ManageServices: 11,
  ViewOwnAgenda: 20,
  ViewTeamAgenda: 21,
  ManageAppointments: 22,
  CancelAnyAppointment: 23,
  ViewCustomers: 30,
  ManageCustomers: 31,
  ViewFinancialReports: 40,
  ManageFinancialEntries: 41,
  ViewOwnCommissions: 42,
  ManagePlatformTenants: 90,
  ManagePlatformPlans: 91,
} as const;
type Permission = keyof typeof codes;
type Profile = Pick<UserDto, "role" | "permissions"> | null;
const admin: Permission[] = [
  "ViewTenantSettings",
  "ManageTenantSettings",
  "ManageTeam",
  "ViewServices",
  "ManageServices",
  "ViewTeamAgenda",
  "ManageAppointments",
  "CancelAnyAppointment",
  "ViewCustomers",
  "ManageCustomers",
  "ViewFinancialReports",
  "ManageFinancialEntries",
];
const defaults: Record<UserRole, Permission[]> = {
  owner: [...admin, "ManageBilling"],
  admin,
  manager: [],
  professional: [
    "ViewServices",
    "ViewOwnAgenda",
    "ManageAppointments",
    "ViewCustomers",
    "ViewOwnCommissions",
  ],
  receptionist: [
    "ViewServices",
    "ViewTeamAgenda",
    "ManageAppointments",
    "CancelAnyAppointment",
    "ViewCustomers",
    "ManageCustomers",
    "ViewFinancialReports",
  ],
  customer: ["ViewServices"],
  platform: ["ManagePlatformTenants", "ManagePlatformPlans"],
};

export function hasPermission(user: Profile, permission: Permission) {
  if (!user?.role || (permission === "ManageBilling" && user.role !== "owner")) return false;
  return user.permissions
    ? user.permissions.includes(permission) || user.permissions.includes(String(codes[permission]))
    : defaults[user.role].includes(permission);
}

export type PanelSection =
  | "dashboard"
  | "agenda"
  | "servicos"
  | "equipe"
  | "clientes"
  | "financeiro"
  | "site"
  | "assinatura"
  | "configuracoes";
const sectionPermissions: Record<Exclude<PanelSection, "dashboard">, Permission[]> = {
  agenda: ["ViewTeamAgenda", "ViewOwnAgenda"],
  servicos: ["ViewServices"],
  equipe: ["ViewTeamAgenda", "ManageTeam"],
  clientes: ["ViewCustomers"],
  financeiro: ["ViewFinancialReports", "ManageFinancialEntries"],
  site: ["ViewTenantSettings"],
  assinatura: ["ViewTenantSettings"],
  configuracoes: ["ViewTenantSettings", "ManageTenantSettings"],
};

export function canAccess(user: Profile, section: PanelSection) {
  return section === "dashboard"
    ? !!user
    : sectionPermissions[section].some((permission) => hasPermission(user, permission));
}

export const ROLE_LABEL: Record<UserRole, string> = {
  owner: "Proprietário",
  admin: "Administrador",
  manager: "Gerente",
  professional: "Profissional",
  receptionist: "Recepção",
  customer: "Cliente",
  platform: "Administrador da plataforma",
};
