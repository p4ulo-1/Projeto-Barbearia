import { loadWorkspace } from "../site-workspace";
import { IS_DEMO } from "./http";
import type { DashboardDto, OnboardingStatus, UserDto } from "./types";
import { onboardingService } from "./onboarding";
import { realOperations, demoOperations, type Appointment } from "./operations";
import { hasPermission } from "@/lib/permissions";
import { publishedSiteUrl } from "./public-site";

const subscriptionLabels: Record<string, string> = {
  Trialing: "Em teste",
  Active: "Ativa",
  PastDue: "Em atraso",
  Cancelled: "Cancelada",
};

function upcoming(
  list: Appointment[] | null,
  today: string,
  now: Date,
  names: Map<string, string>,
): DashboardDto["upcoming"] {
  return (
    list
      ?.filter(
        (item) =>
          item.status === "Confirmed" && Date.parse(`${today}T${item.time}`) >= now.getTime(),
      )
      .sort((a, b) => a.time.localeCompare(b.time))
      .map((item) => ({
        id: item.id,
        startsAt: `${item.date}T${item.time}`,
        status: "confirmed",
        clientName: names.get(item.customerId) ?? "Cliente",
        serviceName: names.get(item.serviceId) ?? "Serviço",
        professionalName: names.get(item.professionalId) ?? "Profissional",
      })) ?? null
  );
}

const attempt = <T>(enabled: boolean, action: () => Promise<T>) =>
  enabled ? action().catch(() => null) : Promise.resolve(null);
const real = {
  async getDashboard(user: UserDto, today: string, now = new Date()): Promise<DashboardDto> {
    const canAgenda = hasPermission(user, "ViewTeamAgenda") || hasPermission(user, "ViewOwnAgenda");
    const canSettings = hasPermission(user, "ViewTenantSettings");
    const [list, clients, services, people, report, site, subscription] = await Promise.all([
      attempt(canAgenda, () => realOperations.appointments(today)),
      attempt(hasPermission(user, "ViewCustomers"), () =>
        realOperations.customers(1, undefined, 100),
      ),
      attempt(canAgenda && hasPermission(user, "ViewServices"), () =>
        realOperations.services(1, undefined, 100),
      ),
      attempt(hasPermission(user, "ViewTeamAgenda"), () => realOperations.professionals(1, 100)),
      attempt(hasPermission(user, "ViewFinancialReports"), () =>
        realOperations.report(today.slice(0, 7) + "-01", today),
      ),
      attempt(canSettings, realOperations.site),
      attempt(canSettings, realOperations.subscription),
    ]);
    const names = new Map(
      [...(clients?.items ?? []), ...(services?.items ?? []), ...(people?.items ?? [])].map(
        (item) => [item.id, item.name],
      ),
    );
    return {
      isNewEstablishment: !!site && !site.isPublished && clients?.totalCount === 0,
      todayAppointments: list?.filter((item) => item.status !== "Cancelled").length ?? null,
      clientsCount: clients?.totalCount ?? null,
      revenue: report
        ? { amount: report.totalIncome, currency: "BRL", periodLabel: "Mês atual" }
        : null,
      topServices: null,
      upcoming: upcoming(list, today, now, names),
      site: {
        status: site ? (site.isPublished ? "published" : "ready") : null,
        publicUrl: site?.isPublished ? publishedSiteUrl(site.subdomain) : undefined,
      },
      subscription: subscription
        ? {
            planName: subscription.planName,
            statusLabel: subscriptionLabels[subscription.status] ?? subscription.status,
          }
        : null,
    };
  },
};

const demo: typeof real = {
  async getDashboard(user, today, now = new Date()) {
    const [session, list, customers, services, people, workspace] = await Promise.all([
      onboardingService.getSession(),
      demoOperations.appointments(today),
      demoOperations.customers(1, undefined, 100),
      demoOperations.services(1, undefined, 100),
      demoOperations.professionals(1, 100),
      loadWorkspace(`demo:${user.id}`).catch(() => undefined),
    ]);
    const names = new Map(
      [...customers.items, ...services.items, ...people.items].map((item) => [item.id, item.name]),
    );
    return {
      isNewEstablishment:
        customers.totalCount === 0 && !workspace?.publication && session.status !== "published",
      todayAppointments: list.filter((item) => item.status !== "Cancelled").length,
      clientsCount: customers.totalCount,
      revenue: null,
      topServices: null,
      upcoming: upcoming(list, today, now, names),
      site: workspace?.publication
        ? {
            status: "published",
            publishedAt: workspace.publication.date,
            publicUrl: `/?siteDemo=${encodeURIComponent(workspace.publication.slug)}`,
          }
        : { status: session.status as OnboardingStatus, publishedAt: session.publishedAt },
      subscription: null,
    };
  },
};

export const panelService = IS_DEMO ? demo : real;
