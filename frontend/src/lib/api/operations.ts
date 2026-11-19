import { ApiError, http, IS_DEMO, queryPath } from "./http";
import type { Page, Section } from "./contracts";
import { tokenStore } from "@/lib/auth/token";
import { userIdFrom } from "./demo/db";

export interface ServiceInput {
  name: string;
  price: number;
  durationMinutes: number;
  description?: string;
  categoryId?: string;
  highlight: boolean;
}
export interface Service extends ServiceInput {
  id: string;
  isActive: boolean;
}
export type ServiceUpdate = Omit<ServiceInput, "price">;
export interface Category {
  id: string;
  name: string;
  sortOrder: number;
}
export interface Combo {
  id: string;
  name: string;
  price: number;
  itemCount: number;
}
export interface ProfessionalInput {
  name: string;
  title?: string;
  bio?: string;
  isServiceProvider: boolean;
  workDays: string[];
  startTime: string;
  endTime: string;
  slotIntervalMinutes: number;
}
export type ProfessionalSchedule = Pick<
  ProfessionalInput,
  "workDays" | "startTime" | "endTime" | "slotIntervalMinutes"
>;
type ApiSchedule = Omit<ProfessionalSchedule, "workDays"> & { workDays: number[] };
export interface Professional {
  id: string;
  name: string;
  title?: string;
  isActive: boolean;
  isServiceProvider: boolean;
}
export interface ProfessionalDetail extends Professional {
  bio?: string | null;
}
export interface ProfessionalUpdate {
  name: string;
  title: string | null;
  bio: string | null;
}
export interface Block {
  id: string;
  start: string;
  end: string;
  reason?: string;
}
export interface Role {
  id: string;
  name: string;
  permissions: string[];
}
export interface CustomerInput {
  name: string;
  phone: string;
  email?: string;
  birthDate?: string;
}
export interface Customer extends CustomerInput {
  id: string;
  isActive: boolean;
}
export interface Preferences {
  customerId: string;
  preferredProfessionalId: string | null;
  notes: string | null;
}
export interface AppointmentInput {
  customerId: string;
  serviceId: string;
  professionalId: string;
  date: string;
  time: string;
}
export interface Appointment extends AppointmentInput {
  id: string;
  durationMinutes: number;
  status: "Confirmed" | "Cancelled" | "Completed";
  cancellationReason?: string;
}
export interface Day {
  dayOfWeek: string;
  isOpen: boolean;
  openTime: string | null;
  closeTime: string | null;
}
export interface Subscription {
  id: string;
  planCode: string;
  planName: string;
  status: string;
  periodStart: string;
  periodEnd: string;
  trialEndsAt: string | null;
  trialStartedAtUtc?: string | null;
  trialEndsAtUtc?: string | null;
  nextChargeAtUtc?: string | null;
  cancellationEffectiveAtUtc?: string | null;
  pastDueSince?: string | null;
  cancellationRequested?: boolean;
  billingAccess?: string | null;
  firstPaymentStatus?: "Scheduled" | "Pending" | "Rejected" | "Paid" | null;
}
export interface Checkout {
  checkoutKey: string;
  attemptId: string;
  state: string;
  trialStartsAt: string | null;
  trialEndsAt: string | null;
  planCode?: string | null;
}
export interface Lifecycle {
  subscriptionId: string;
  state: string;
  status: string;
  nextChargeAt: string | null;
  pastDueSince: string | null;
  cancellationEffectiveAt: string | null;
}

export interface PlanChangeOperation {
  id: string;
  targetPlanCode: string;
  reason: "Upgrade" | "Downgrade";
  state: string;
  effectiveAt?: string | null;
  cycleEnd?: string | null;
  sourcePrice?: number | null;
  targetPrice?: number | null;
  additionalAmount?: number | null;
  quoteExpiresAt?: string | null;
  paymentApproved: boolean;
  recurrenceConfirmed: boolean;
  issue?: string | null;
}
export interface Invoice {
  id: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  status: string;
  total: number;
}
export interface Usage {
  resource: string;
  included: number;
  consumed: number;
  purchasedExtra: number;
  remaining: number;
  consumedTokens?: number;
}
export interface PlanChange {
  id: string;
  fromPlanId: string | null;
  toPlanId: string;
  reason: string;
  changedAt: string;
}
export interface Site {
  id: string;
  subdomain: string;
  status: string;
  isPublished: boolean;
  pageCount: number;
  publicationCount: number;
}
export interface SitePage {
  slug: string;
  title: string;
  order?: number;
  sortOrder?: number;
  sections: Section[];
}
export interface Publication {
  id: string;
  version: number;
  publishedAt: string;
}
export interface Transaction {
  id: string;
  type: string;
  amount: number;
  description: string;
}
export interface Payment {
  id: string;
  amount: number;
  method: string;
  status: string;
}
export interface FinanceReport {
  periodStart: string;
  periodEnd: string;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  averageTicket: number;
  appointmentCount: number;
}
export interface Commission {
  professionalId: string;
  pending: number;
  approved: number;
  paid: number;
  receivable: number;
}
export interface Package {
  id: string;
  name: string;
  price: number;
  sessionsIncluded: number;
  validityDays: number;
}
export interface MembershipPlan {
  id: string;
  name: string;
  monthlyPrice: number;
}
export interface Conversation {
  id: string;
  phoneNumber: string;
  status: string;
  messageCount: number;
}
export interface ConversationDetail extends Conversation {
  assignedTo: string | null;
  messages: { id: string; direction: string; body: string; sentAt: string }[];
}
export interface Template {
  id: string;
  key: string;
  messageType: string;
  body: string;
  isActive: boolean;
}
export interface Notification {
  id: string;
  messageType: string;
  recipient: string;
  scheduledAt: string;
  status: string;
}

const root = "/api/v1";
const get = <T>(path: string, query: Record<string, string | number | boolean | undefined> = {}) =>
  http<T>(queryPath(root + path, query));
const send = <T>(path: string, method: string, json?: unknown) =>
  http<T>(root + path, { method, json });
const paged = <T>(path: string, page = 1, query = {}, pageSize = 20) =>
  get<Page<T>>(path, { ...query, page, pageSize });
const catalog = "/catalog";
const pros = "/team/professionals";
const roles = "/team/roles";
const customers = "/customers";
const appointments = "/agenda/appointments";
const finance = "/finance";
const billing = "/billing";
const schedule = "/tenant/settings/schedule";

export const realOperations = {
  services: (page = 1, onlyActive?: boolean, pageSize = 20) =>
    paged<Service>(catalog + "/services", page, { onlyActive }, pageSize),
  createService: (input: ServiceInput) => send<Service>(catalog + "/services", "POST", input),
  service: (id: string) => get<Service>(`${catalog}/services/${id}`),
  updateService: (id: string, input: ServiceUpdate) =>
    send<Service>(`${catalog}/services/${id}`, "PUT", input),
  activateService: (id: string) => send<void>(`${catalog}/services/${id}/activate`, "POST"),
  price: (id: string, price: number) =>
    send<Service>(`${catalog}/services/${id}/price`, "PATCH", { price }),
  deactivate: (id: string) => send<void>(`${catalog}/services/${id}/deactivate`, "POST"),
  categories: () => get<Category[]>(catalog + "/categories"),
  createCategory: (name: string, sortOrder: number) =>
    send<Category>(catalog + "/categories", "POST", { name, sortOrder }),
  combos: () => get<Combo[]>(catalog + "/combos"),
  createCombo: (name: string, price: number, items: { serviceId: string; quantity: number }[]) =>
    send<Combo>(catalog + "/combos", "POST", { name, price, items }),
  professionals: (page = 1, pageSize = 20, includeInactive = false) =>
    paged<Professional>(pros, page, { includeInactive }, pageSize),
  professional: (id: string) => get<ProfessionalDetail>(`${pros}/${id}`),
  updatePro: (id: string, input: ProfessionalUpdate) =>
    send<ProfessionalDetail>(`${pros}/${id}`, "PUT", input),
  activatePro: (id: string) => send<void>(`${pros}/${id}/activate`, "POST"),
  deactivatePro: (id: string) => send<void>(`${pros}/${id}/deactivate`, "POST"),
  createPro: (input: ProfessionalInput) =>
    send<Professional>(pros, "POST", {
      ...input,
      workDays: toApiDays(input.workDays),
    }),
  professionalSchedule: async (id: string): Promise<ProfessionalSchedule> =>
    fromApiSchedule(await get<ApiSchedule>(`${pros}/${id}/schedule`)),
  updateSchedule: async (id: string, input: ProfessionalSchedule): Promise<ProfessionalSchedule> =>
    fromApiSchedule(
      await send<ApiSchedule>(`${pros}/${id}/schedule`, "PUT", {
        ...input,
        workDays: toApiDays(input.workDays),
      }),
    ),
  setProvider: (id: string, isServiceProvider: boolean) =>
    send<Professional>(`${pros}/${id}/service-provider`, "PATCH", { isServiceProvider }),
  professionalServices: (id: string) => get<string[]>(`${pros}/${id}/services`),
  assignService: (id: string, serviceId: string) =>
    send<void>(`${pros}/${id}/services/${serviceId}`, "POST"),
  unassignService: (id: string, serviceId: string) =>
    send<void>(`${pros}/${id}/services/${serviceId}`, "DELETE"),
  blocks: (id: string) => get<Block[]>(`${pros}/${id}/blocks`),
  blockDetail: (id: string, blockId: string) => get<Block>(`${pros}/${id}/blocks/${blockId}`),
  deleteBlock: (id: string, blockId: string) =>
    send<void>(`${pros}/${id}/blocks/${blockId}`, "DELETE"),
  block: (id: string, input: Omit<Block, "id">) =>
    send<Block>(`${pros}/${id}/blocks`, "POST", input),
  roles: () => get<Role[]>(roles),
  assignable: () => get<string[]>(roles + "/assignable-permissions"),
  saveRole: (input: Omit<Role, "id">, id?: string) =>
    send<Role>(id ? `${roles}/${id}` : roles, id ? "PUT" : "POST", input),
  deleteRole: (id: string) => send<void>(`${roles}/${id}`, "DELETE"),
  assignRole: (id: string, userId: string, assigned: boolean) =>
    send<void>(`${roles}/${id}/users/${userId}`, assigned ? "PUT" : "DELETE"),
  customers: (page = 1, search?: string, pageSize = 20) =>
    paged<Customer>(customers, page, { search }, pageSize),
  customer: (id: string) => get<Customer>(`${customers}/${id}`),
  createCustomer: (input: CustomerInput) => send<Customer>(customers, "POST", input),
  anonymize: (id: string) => send<void>(`${customers}/${id}/anonymize`, "POST"),
  preferences: (id: string) => get<Preferences>(`${customers}/${id}/preferences`),
  setPreferences: (id: string, input: Omit<Preferences, "customerId">) =>
    send<Preferences>(`${customers}/${id}/preferences`, "PUT", input),
  appointments: (date: string, professionalId?: string) =>
    get<Appointment[]>(appointments, { date, professionalId }),
  ownProfessional: () => get<{ professionalId: string }>("/agenda/me"),
  ownAppointments: (date: string) => get<Appointment[]>("/agenda/mine/appointments", { date }),
  ownAvailability: (date: string, serviceId: string) =>
    get<{ availableSlots: string[] }>("/agenda/mine/availability", { date, serviceId }),
  availability: (professionalId: string, date: string, serviceId: string) =>
    get<{ availableSlots: string[] }>("/agenda/availability", {
      professionalId,
      date,
      serviceId,
    }),
  createAppointment: (input: AppointmentInput) => send<Appointment>(appointments, "POST", input),
  rescheduleAvailability: (id: string, date: string) =>
    get<{ availableSlots: string[] }>(`${appointments}/${id}/reschedule-availability`, { date }),
  reschedule: (id: string, date: string, time: string) =>
    send<Appointment>(`${appointments}/${id}/reschedule`, "PATCH", { date, time }),
  cancel: (id: string, reason?: string) =>
    send<Appointment>(`${appointments}/${id}/cancel`, "POST", reason ? { reason } : undefined),
  cancelOwn: (id: string, reason?: string) =>
    send<Appointment>(
      `/agenda/mine/appointments/${id}/cancel`,
      "POST",
      reason ? { reason } : undefined,
    ),
  complete: (id: string) => send<Appointment>(`${appointments}/${id}/complete`, "POST"),
  schedule: () => get<{ days: Day[] }>(schedule),
  saveSchedule: (days: Day[]) => send<{ days: Day[] }>(schedule, "PUT", { days }),
  subscription: () => get<Subscription>(billing + "/subscription"),
  invoices: (page = 1) => paged<Invoice>(billing + "/invoices", page),
  usage: () => get<Usage[]>(billing + "/usage"),
  planHistory: () => get<PlanChange[]>(billing + "/subscription/history"),
  planChangeOperation: async () =>
    (await get<PlanChangeOperation | undefined>(billing + "/subscription/plan-change")) ?? null,
  requestPlanChange: (planCode: string, operationKey: string) =>
    send<PlanChangeOperation>(billing + "/subscription/plan-change", "POST", {
      planCode,
      operationKey,
    }),
  confirmPlanChangePayment: (id: string, cardToken: string, paymentMethod: string) =>
    send<PlanChangeOperation>(`${billing}/subscription/plan-change/${id}/payment`, "POST", {
      cardToken,
      paymentMethod,
    }),
  upgradeSuggestion: () =>
    get<
      { currentPlanCode: string; requiredPlanCode: string; professionalCount: number } | undefined
    >(billing + "/subscription/upgrade-suggestion"),
  changePlan: (planCode: string, existing: boolean) =>
    send<Subscription>(billing + "/subscription", existing ? "PATCH" : "POST", { planCode }),
  currentCheckout: async () =>
    (await get<Checkout | undefined>(billing + "/subscription/checkout/current")) ?? null,
  checkout: (key: string) => get<Checkout>(`${billing}/subscription/checkout/${key}`),
  startCheckout: (checkoutKey: string, planCode: string, cardToken?: string) =>
    send<Checkout>(billing + "/subscription/checkout", "POST", {
      checkoutKey,
      planCode,
      ...(cardToken ? { cardToken } : {}),
    }),
  verifyCheckout: (key: string) =>
    send<Checkout>(`${billing}/subscription/checkout/${key}/verify`, "POST"),
  verifySubscription: () => send<Lifecycle>(billing + "/subscription/verify", "POST"),
  cancelPlan: () => send<Lifecycle>(billing + "/subscription/cancel", "POST"),
  transactions: (page = 1, from?: string, to?: string) =>
    paged<Transaction>(finance + "/transactions", page, { from, to }),
  report: (from: string, to: string) => get<FinanceReport>(finance + "/report", { from, to }),
  commissions: (professionalId: string, from: string, to: string) =>
    get<Commission>(`${finance}/commissions/${professionalId}`, { from, to }),
  payment: (amount: number, method: number, appointmentId?: string) =>
    send<Payment>(finance + "/payments", "POST", { amount, method, appointmentId }),
  refund: (id: string, amount: number) =>
    send<Payment>(`${finance}/payments/${id}/refund`, "POST", { amount }),
  transaction: (input: { type: number; amount: number; description: string }) =>
    send<Transaction>(finance + "/transactions", "POST", input),
  site: () => get<Site>("/site"),
  sitePreview: async () => {
    const snapshot = await http<string>(root + "/site/preview", { response: "text" });
    const decoded = JSON.parse(snapshot) as string | { theme?: unknown; pages?: SitePage[] };
    return (typeof decoded === "string" ? JSON.parse(decoded) : decoded) as {
      theme?: unknown;
      pages?: SitePage[];
    };
  },
  publishSite: () => send<Publication>("/site/publish", "POST"),
  rollbackSite: (publicationId: string) => send<void>(`/site/rollback/${publicationId}`, "POST"),
  packages: () => get<Package[]>("/modules/packages"),
  createPackage: (input: Omit<Package, "id">) => send<Package>("/modules/packages", "POST", input),
  membershipPlans: () => get<MembershipPlan[]>("/modules/membership-plans"),
  createMembershipPlan: (
    input: Omit<MembershipPlan, "id"> & {
      benefits: { serviceId?: string; quantityPerPeriod?: number; discountPercentage?: number }[];
    },
  ) => send<MembershipPlan>("/modules/membership-plans", "POST", input),
  subscribeCustomer: (input: {
    customerId: string;
    membershipPlanId: string;
    periodStart: string;
    periodEnd: string;
  }) => send<unknown>("/modules/memberships", "POST", input),
  loyalty: (customerId: string) => get<{ points: number }>(`/modules/loyalty/${customerId}`),
  addPoints: (customerId: string, points: number) =>
    send<unknown>(`/modules/loyalty/${customerId}/earn`, "POST", { points }),
  review: (input: { appointmentId: string; customerId: string; score: number; comment?: string }) =>
    send<unknown>("/modules/reviews", "POST", input),
  nps: (from: string, to: string) =>
    get<{ totalResponses: number; score: number }>("/modules/nps", { from, to }),
  conversations: (page = 1) => paged<Conversation>("/whatsapp/conversations", page),
  conversation: (id: string) => get<ConversationDetail>(`/whatsapp/conversations/${id}`),
  escalate: (id: string, userId: string) =>
    send<void>(`/whatsapp/conversations/${id}/escalate`, "POST", { userId }),
  templates: () => get<Template[]>("/whatsapp/templates"),
  createTemplate: (key: string, messageType: number | string, body: string) =>
    send<Template>("/whatsapp/templates", "POST", { key, messageType, body }),
  policy: () => get<{ allowedTypes: (number | string)[] }>("/whatsapp/policy"),
  notifications: (page = 1) => paged<Notification>("/whatsapp/notifications", page),
};

interface DemoData {
  services: Service[];
  categories: Category[];
  combos: Combo[];
  professionals: (Professional & ProfessionalInput)[];
  servicesByProfessional: Record<string, string[]>;
  blocksByProfessional: Record<string, Block[]>;
  customers: Customer[];
  preferences: Record<string, Preferences>;
  appointments: Appointment[];
  schedule: Day[];
}
function demoData(id = userIdFrom(tokenStore.get())) {
  if (!id) throw new ApiError("Entre em uma conta de demonstração.", 401);
  const key = `servia.demo-operations.v1.${id}`;
  const stored = localStorage.getItem(key);
  const data: DemoData = stored
    ? (JSON.parse(stored) as DemoData)
    : {
        services: [],
        categories: [],
        combos: [],
        professionals: [],
        servicesByProfessional: {},
        blocksByProfessional: {},
        customers: [],
        preferences: {},
        appointments: [],
        schedule: [],
      };
  return { data, save: () => localStorage.setItem(key, JSON.stringify(data)) };
}
const list = <T>(items: T[], page = 1, pageSize = 20): Page<T> => ({
  items: items.slice((page - 1) * pageSize, page * pageSize),
  page,
  pageSize,
  totalCount: items.length,
  totalPages: Math.ceil(items.length / pageSize),
  hasPrevious: page > 1,
  hasNext: page * pageSize < items.length,
});
const find = <T extends { id: string }>(items: T[], id: string) => {
  const item = items.find((entry) => entry.id === id);
  if (!item) throw new ApiError("Registro não encontrado.", 404);
  return item;
};
function change<T>(action: (data: DemoData) => T) {
  const { data, save } = demoData();
  const result = action(data);
  save();
  return result;
}
const id = () => crypto.randomUUID();
const timeMinutes = (value: string) => Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5));
export const daysOfWeek = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];
const toApiDays = (days: string[]) => days.map((day) => daysOfWeek.indexOf(day));
const fromApiSchedule = (schedule: ApiSchedule): ProfessionalSchedule => ({
  ...schedule,
  workDays: schedule.workDays.map((day) => {
    const name = daysOfWeek[day];
    if (!name) throw new Error("A API retornou um dia de jornada inválido.");
    return name;
  }),
});
function slots(
  data: DemoData,
  professionalId: string,
  date: string,
  duration: number,
  exclude?: string,
) {
  const professional = find(data.professionals, professionalId);
  const day = daysOfWeek[new Date(`${date}T12:00:00`).getDay()];
  if (!day || !professional.workDays.includes(day) || !professional.isServiceProvider) return [];
  const available = [];
  for (
    let minute = timeMinutes(professional.startTime);
    minute + duration <= timeMinutes(professional.endTime);
    minute += professional.slotIntervalMinutes
  ) {
    const time = `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}:00`;
    const begin = Date.parse(`${date}T${time}`);
    const overlap = data.appointments.some(
      (item) =>
        item.id !== exclude &&
        item.professionalId === professionalId &&
        item.date === date &&
        item.status === "Confirmed" &&
        timeMinutes(item.time) < minute + duration &&
        timeMinutes(item.time) + item.durationMinutes > minute,
    );
    const blocked = (data.blocksByProfessional[professionalId] ?? []).some(
      (block) =>
        Date.parse(block.start) < begin + duration * 60_000 && Date.parse(block.end) > begin,
    );
    if (!overlap && !blocked) available.push(time);
  }
  return available;
}
const unavailable = async (): Promise<never> => {
  throw new ApiError("Este recurso fica disponível ao conectar a API do Servia.", 501);
};

export const demoOperations: typeof realOperations = {
  services: async (page, active, pageSize) =>
    list(
      demoData().data.services.filter((item) => !active || item.isActive),
      page,
      pageSize,
    ),
  createService: async (input) =>
    change((data) => {
      const service = { ...input, id: id(), isActive: true };
      data.services.push(service);
      return service;
    }),
  service: async (serviceId) => find(demoData().data.services, serviceId),
  updateService: async (serviceId, input) =>
    change((data) => Object.assign(find(data.services, serviceId), input)),
  activateService: async (serviceId) =>
    change((data) => {
      find(data.services, serviceId).isActive = true;
    }),
  price: async (serviceId, price) =>
    change((data) => Object.assign(find(data.services, serviceId), { price })),
  deactivate: async (serviceId) =>
    change((data) => {
      find(data.services, serviceId).isActive = false;
    }),
  categories: async () => demoData().data.categories,
  createCategory: async (name, sortOrder) =>
    change((data) => {
      const item = { id: id(), name, sortOrder };
      data.categories.push(item);
      return item;
    }),
  combos: async () => demoData().data.combos,
  createCombo: async (name, price, items) =>
    change((data) => {
      const item = { id: id(), name, price, itemCount: items.length };
      data.combos.push(item);
      return item;
    }),
  professionals: async (page, pageSize, includeInactive) =>
    list(
      demoData().data.professionals.filter((p) => includeInactive || p.isActive),
      page,
      pageSize,
    ),
  professional: async (professionalId) => find(demoData().data.professionals, professionalId),
  updatePro: async (professionalId, input) =>
    change((data) =>
      Object.assign(find(data.professionals, professionalId), {
        ...input,
        title: input.title ?? "",
        bio: input.bio ?? "",
      }),
    ),
  activatePro: async (professionalId) =>
    change((data) => {
      find(data.professionals, professionalId).isActive = true;
    }),
  deactivatePro: async (professionalId) =>
    change((data) => {
      find(data.professionals, professionalId).isActive = false;
    }),
  createPro: async (input) =>
    change((data) => {
      if (!input.workDays.length || input.startTime >= input.endTime)
        throw new ApiError(
          "Informe dias de trabalho e um horário de término posterior ao início.",
          400,
        );
      const item = { ...input, id: id(), isActive: true };
      data.professionals.push(item);
      return item;
    }),
  professionalSchedule: async (professionalId) => {
    const p = find(demoData().data.professionals, professionalId);
    return {
      workDays: p.workDays,
      startTime: p.startTime,
      endTime: p.endTime,
      slotIntervalMinutes: p.slotIntervalMinutes,
    };
  },
  updateSchedule: async (professionalId, input) =>
    change((data) => {
      if (
        input.startTime >= input.endTime ||
        input.slotIntervalMinutes < 5 ||
        input.slotIntervalMinutes > 120
      )
        throw new ApiError("Informe uma jornada válida.", 400);
      Object.assign(find(data.professionals, professionalId), input);
      return input;
    }),
  setProvider: async (professionalId, isServiceProvider) =>
    change((data) =>
      Object.assign(find(data.professionals, professionalId), { isServiceProvider }),
    ),
  professionalServices: async (professionalId) =>
    demoData().data.servicesByProfessional[professionalId] ?? [],
  assignService: async (professionalId, serviceId) =>
    change((data) => {
      data.servicesByProfessional[professionalId] = [
        ...new Set([...(data.servicesByProfessional[professionalId] ?? []), serviceId]),
      ];
    }),
  unassignService: async (professionalId, serviceId) =>
    change((data) => {
      data.servicesByProfessional[professionalId] = (
        data.servicesByProfessional[professionalId] ?? []
      ).filter((id) => id !== serviceId);
    }),
  blocks: async (professionalId) => demoData().data.blocksByProfessional[professionalId] ?? [],
  blockDetail: async (professionalId, blockId) =>
    find(demoData().data.blocksByProfessional[professionalId] ?? [], blockId),
  deleteBlock: async (professionalId, blockId) =>
    change((data) => {
      const blocks = data.blocksByProfessional[professionalId] ?? [];
      find(blocks, blockId);
      data.blocksByProfessional[professionalId] = blocks.filter((block) => block.id !== blockId);
    }),
  block: async (professionalId, input) =>
    change((data) => {
      if (Date.parse(input.end) <= Date.parse(input.start))
        throw new ApiError("O término deve ser posterior ao início.", 400);
      const item = { ...input, id: id() };
      (data.blocksByProfessional[professionalId] ??= []).push(item);
      return item;
    }),
  roles: unavailable,
  assignable: unavailable,
  saveRole: unavailable,
  deleteRole: unavailable,
  assignRole: unavailable,
  customers: async (page, search, pageSize) =>
    list(
      demoData().data.customers.filter(
        (item) =>
          !search || `${item.name} ${item.phone}`.toLowerCase().includes(search.toLowerCase()),
      ),
      page,
      pageSize,
    ),
  customer: async (customerId) => find(demoData().data.customers, customerId),
  createCustomer: async (input) =>
    change((data) => {
      const item = { ...input, id: id(), isActive: true };
      data.customers.push(item);
      return item;
    }),
  anonymize: async (customerId) =>
    change((data) => {
      const item = find(data.customers, customerId);
      Object.assign(item, { name: "Cliente anonimizado", phone: "", isActive: false });
      delete item.email;
      delete item.birthDate;
      delete data.preferences[customerId];
    }),
  preferences: async (customerId) =>
    demoData().data.preferences[customerId] ?? {
      customerId,
      preferredProfessionalId: null,
      notes: null,
    },
  setPreferences: async (customerId, input) =>
    change((data) => {
      if (!find(data.customers, customerId).isActive)
        throw new ApiError("O cadastro foi anonimizado.", 400);
      return (data.preferences[customerId] = { customerId, ...input });
    }),
  appointments: async (date, professionalId) =>
    demoData().data.appointments.filter(
      (item) => item.date === date && (!professionalId || item.professionalId === professionalId),
    ),
  ownProfessional: unavailable,
  ownAppointments: unavailable,
  ownAvailability: unavailable,
  availability: async (professionalId, date, serviceId) => {
    const data = demoData().data;
    const service = demoBookingService(data, serviceId, professionalId);
    return {
      availableSlots: slots(data, professionalId, date, service.durationMinutes),
    };
  },
  createAppointment: async (input) =>
    change((data) => {
      const service = demoBookingService(data, input.serviceId, input.professionalId);
      if (
        !slots(data, input.professionalId, input.date, service.durationMinutes).includes(input.time)
      )
        throw new ApiError("Horário indisponível.", 409);
      const item: Appointment = {
        ...input,
        id: id(),
        durationMinutes: service.durationMinutes,
        status: "Confirmed",
      };
      data.appointments.push(item);
      return item;
    }),
  rescheduleAvailability: async (appointmentId, date) => {
    const data = demoData().data;
    const item = find(data.appointments, appointmentId);
    demoBookingService(data, item.serviceId, item.professionalId);
    return {
      availableSlots: slots(data, item.professionalId, date, item.durationMinutes, item.id),
    };
  },
  reschedule: async (appointmentId, date, time) =>
    change((data) => {
      const item = find(data.appointments, appointmentId);
      if (
        item.status !== "Confirmed" ||
        !slots(data, item.professionalId, date, item.durationMinutes, item.id).includes(time)
      )
        throw new ApiError("Horário indisponível.", 409);
      return Object.assign(item, { date, time });
    }),
  cancel: async (appointmentId, reason) =>
    change((data) => {
      const item = find(data.appointments, appointmentId);
      if (item.status !== "Confirmed")
        throw new ApiError("Somente agendamentos confirmados podem ser cancelados.", 400);
      return Object.assign(item, {
        status: "Cancelled" as const,
        ...(reason ? { cancellationReason: reason } : {}),
      });
    }),
  cancelOwn: unavailable,
  complete: async (appointmentId) =>
    change((data) => {
      const item = find(data.appointments, appointmentId);
      if (item.status !== "Confirmed" || Date.parse(`${item.date}T${item.time}`) > Date.now())
        throw new ApiError("O atendimento ainda não pode ser concluído.", 400);
      return Object.assign(item, { status: "Completed" as const });
    }),
  schedule: async () => ({ days: demoData().data.schedule }),
  saveSchedule: async (days) =>
    change((data) => {
      if (
        days.some(
          (day) => day.isOpen && (!day.openTime || !day.closeTime || day.openTime >= day.closeTime),
        )
      )
        throw new ApiError("Confira os horários de abertura e fechamento.", 400);
      data.schedule = days;
      return { days };
    }),
  subscription: unavailable,
  invoices: unavailable,
  usage: unavailable,
  planHistory: unavailable,
  upgradeSuggestion: unavailable,
  planChangeOperation: unavailable,
  requestPlanChange: unavailable,
  confirmPlanChangePayment: unavailable,
  changePlan: unavailable,
  currentCheckout: unavailable,
  checkout: unavailable,
  startCheckout: unavailable,
  verifyCheckout: unavailable,
  verifySubscription: unavailable,
  cancelPlan: unavailable,
  transactions: unavailable,
  report: unavailable,
  commissions: unavailable,
  payment: unavailable,
  refund: unavailable,
  transaction: unavailable,
  site: unavailable,
  sitePreview: unavailable,
  publishSite: unavailable,
  rollbackSite: unavailable,
  packages: unavailable,
  createPackage: unavailable,
  membershipPlans: unavailable,
  createMembershipPlan: unavailable,
  subscribeCustomer: unavailable,
  loyalty: unavailable,
  addPoints: unavailable,
  review: unavailable,
  nps: unavailable,
  conversations: unavailable,
  conversation: unavailable,
  escalate: unavailable,
  templates: unavailable,
  createTemplate: unavailable,
  policy: unavailable,
  notifications: unavailable,
};

export const operations = IS_DEMO ? demoOperations : realOperations;

function demoBookingService(data: DemoData, serviceId: string, professionalId: string) {
  const service = find(data.services, serviceId);
  const professional = find(data.professionals, professionalId);
  if (
    !service.isActive ||
    !professional.isActive ||
    !professional.isServiceProvider ||
    !data.servicesByProfessional[professionalId]?.includes(serviceId)
  ) {
    throw new ApiError("Agendamento online indisponível para este atendimento.", 404);
  }
  return service;
}

function futureSlots(
  data: DemoData,
  professionalId: string,
  date: string,
  durationMinutes: number,
) {
  return slots(data, professionalId, date, durationMinutes).filter(
    (time) => Date.parse(date + "T" + time) > Date.now(),
  );
}

export const demoPublicData = {
  services: (owner: string) =>
    demoData(owner)
      .data.services.filter((entry) => entry.isActive)
      .map(({ id, name, price, description, durationMinutes }) => ({
        id,
        name,
        price,
        description: description ?? null,
        durationMinutes,
        currency: "BRL",
      })),
  professionals(owner: string) {
    const { data } = demoData(owner);
    const active = new Set(
      data.services.filter((entry) => entry.isActive).map((entry) => entry.id),
    );
    return data.professionals
      .filter((entry) => entry.isActive && entry.isServiceProvider)
      .map(({ id, name }) => {
        const serviceIds = (data.servicesByProfessional[id] ?? []).filter((serviceId) =>
          active.has(serviceId),
        );
        return { id, name, serviceIds };
      });
  },
  availability(owner: string, serviceId: string, professionalId: string, date: string) {
    const { data } = demoData(owner);
    const service = demoBookingService(data, serviceId, professionalId);
    return { slots: futureSlots(data, professionalId, date, service.durationMinutes) };
  },
  book(owner: string, input: import("./public-site").Booking) {
    const { data, save } = demoData(owner);
    const { serviceId, professionalId, date, time } = input;
    const service = demoBookingService(data, serviceId, professionalId);
    const durationMinutes = service.durationMinutes;
    const phone = input.phone.replace(/\D/g, "");
    if (!input.name.trim() || phone.length < 8)
      throw new ApiError("Informe seu nome e um telefone válido.", 400);
    if (!futureSlots(data, professionalId, date, durationMinutes).includes(time))
      throw new ApiError("Esse horário acabou de ficar indisponível.", 409);
    const customer = data.customers.find(
      (entry) => entry.isActive && entry.phone.replace(/\D/g, "") === phone,
    ) ?? {
      id: id(),
      name: input.name,
      phone: input.phone,
      isActive: true,
      ...(input.email ? { email: input.email } : {}),
    };
    if (!data.customers.includes(customer)) data.customers.push(customer);
    const appointment: Appointment = {
      id: id(),
      customerId: customer.id,
      serviceId,
      professionalId,
      date,
      time,
      durationMinutes,
      status: "Confirmed",
    };
    data.appointments.push(appointment);
    save();
    return { id: appointment.id, date, time, durationMinutes };
  },
};
