/**
 * Serviços de API por área de negócio (F14.1).
 *
 * Cada função corresponde a um endpoint. Os componentes consomem estas
 * funções, nunca `fetch` diretamente.
 */

import { buildQuery, type ApiClient, type PagedResponse } from './client';
import type {
  Appointment,
  Availability,
  Category,
  Conversation,
  Customer,
  FinancialReport,
  LoyaltyAccount,
  NpsReport,
  Payment,
  Plan,
  Professional,
  Service,
  Site,
  Subscription,
} from './types';

export function createCatalogApi(client: ApiClient) {
  return {
    listServices: (page = 1, pageSize = 20) =>
      client.request<PagedResponse<Service>>(
        'GET',
        `/api/v1/catalog/services${buildQuery({ page, pageSize })}`,
      ),

    createService: (input: {
      name: string;
      price: number;
      durationMinutes: number;
      description?: string;
    }) => client.request<Service>('POST', '/api/v1/catalog/services', input),

    changePrice: (id: string, price: number) =>
      client.request<Service>('PATCH', `/api/v1/catalog/services/${id}/price`, { price }),

    deactivate: (id: string) =>
      client.request<void>('POST', `/api/v1/catalog/services/${id}/deactivate`),

    listCategories: () => client.request<readonly Category[]>('GET', '/api/v1/catalog/categories'),

    createCategory: (name: string, sortOrder = 0) =>
      client.request<Category>('POST', '/api/v1/catalog/categories', { name, sortOrder }),
  };
}

export function createTeamApi(client: ApiClient) {
  return {
    listProfessionals: (page = 1, pageSize = 20) =>
      client.request<PagedResponse<Professional>>(
        'GET',
        `/api/v1/team/professionals${buildQuery({ page, pageSize })}`,
      ),

    createProfessional: (input: {
      name: string;
      workDays: readonly number[];
      startTime: string;
      endTime: string;
      slotIntervalMinutes?: number;
      title?: string;
    }) => client.request<Professional>('POST', '/api/v1/team/professionals', input),

    linkService: (professionalId: string, serviceId: string) =>
      client.request<void>(
        'POST',
        `/api/v1/team/professionals/${professionalId}/services/${serviceId}`,
      ),

    createBlock: (professionalId: string, start: string, end: string, reason?: string) =>
      client.request<{ id: string }>(
        'POST',
        `/api/v1/team/professionals/${professionalId}/blocks`,
        { start, end, reason },
      ),
  };
}

export function createCustomersApi(client: ApiClient) {
  return {
    list: (page = 1, pageSize = 20, search?: string) =>
      client.request<PagedResponse<Customer>>(
        'GET',
        `/api/v1/customers${buildQuery({ page, pageSize, search })}`,
      ),

    get: (id: string) => client.request<Customer>('GET', `/api/v1/customers/${id}`),

    create: (input: { name: string; phone: string; email?: string }) =>
      client.request<Customer>('POST', '/api/v1/customers', input),

    anonymize: (id: string) =>
      client.request<void>('POST', `/api/v1/customers/${id}/anonymize`),
  };
}

export function createAgendaApi(client: ApiClient) {
  return {
    availability: (professionalId: string, date: string, durationMinutes: number) =>
      client.request<Availability>(
        'GET',
        `/api/v1/agenda/availability${buildQuery({ professionalId, date, durationMinutes })}`,
      ),

    createAppointment: (input: {
      customerId: string;
      serviceId: string;
      professionalId: string;
      date: string;
      time: string;
      durationMinutes: number;
    }) => client.request<Appointment>('POST', '/api/v1/agenda/appointments', input),

    listAppointments: (date: string, professionalId?: string) =>
      client.request<readonly Appointment[]>(
        'GET',
        `/api/v1/agenda/appointments${buildQuery({ date, professionalId })}`,
      ),

    cancel: (id: string, reason?: string) =>
      client.request<Appointment>('POST', `/api/v1/agenda/appointments/${id}/cancel`, { reason }),

    complete: (id: string) =>
      client.request<Appointment>('POST', `/api/v1/agenda/appointments/${id}/complete`),
  };
}

export function createFinanceApi(client: ApiClient) {
  return {
    registerPayment: (amount: number, method: string, appointmentId?: string) =>
      client.request<Payment>('POST', '/api/v1/finance/payments', {
        amount,
        method,
        appointmentId,
      }),

    refund: (id: string, amount: number) =>
      client.request<Payment>('POST', `/api/v1/finance/payments/${id}/refund`, { amount }),

    report: (from: string, to: string) =>
      client.request<FinancialReport>(
        'GET',
        `/api/v1/finance/report${buildQuery({ from, to })}`,
      ),

    commissionStatement: (professionalId: string, from: string, to: string) =>
      client.request<{
        professionalId: string;
        pending: number;
        approved: number;
        paid: number;
        receivable: number;
      }>(
        'GET',
        `/api/v1/finance/commissions/${professionalId}${buildQuery({ from, to })}`,
      ),
  };
}

export function createBillingApi(client: ApiClient) {
  return {
    subscription: () => client.request<Subscription>('GET', '/api/v1/billing/subscription'),

    invoices: (page = 1, pageSize = 20) =>
      client.request<PagedResponse<{ id: string; total: number; status: string }>>(
        'GET',
        `/api/v1/billing/invoices${buildQuery({ page, pageSize })}`,
      ),

    usage: () =>
      client.request<readonly { resource: string; included: number; consumed: number; remaining: number }[]>(
        'GET',
        '/api/v1/billing/usage',
      ),

    plans: () => client.request<readonly Plan[]>('GET', '/api/v1/billing/plans'),
  };
}

export function createSiteApi(client: ApiClient) {
  return {
    get: () => client.request<Site>('GET', '/api/v1/site'),

    create: (subdomain: string) => client.request<Site>('POST', '/api/v1/site', { subdomain }),

    preview: () => client.request<string>('GET', '/api/v1/site/preview'),

    publish: () =>
      client.request<{ id: string; version: number; publishedAt: string }>(
        'POST',
        '/api/v1/site/publish',
      ),

    rollback: (publicationId: string) =>
      client.request<void>('POST', `/api/v1/site/rollback/${publicationId}`),
  };
}

export function createWhatsAppApi(client: ApiClient) {
  return {
    conversations: (page = 1, pageSize = 20) =>
      client.request<PagedResponse<Conversation>>(
        'GET',
        `/api/v1/whatsapp/conversations${buildQuery({ page, pageSize })}`,
      ),

    escalate: (id: string, userId: string) =>
      client.request<void>('POST', `/api/v1/whatsapp/conversations/${id}/escalate`, { userId }),

    templates: () =>
      client.request<readonly { id: string; key: string; body: string; isActive: boolean }[]>(
        'GET',
        '/api/v1/whatsapp/templates',
      ),
  };
}

export function createModulesApi(client: ApiClient) {
  return {
    loyalty: (customerId: string) =>
      client.request<LoyaltyAccount>('GET', `/api/v1/modules/loyalty/${customerId}`),

    earnPoints: (customerId: string, points: number) =>
      client.request<LoyaltyAccount>('POST', `/api/v1/modules/loyalty/${customerId}/earn`, {
        points,
      }),

    nps: (from: string, to: string) =>
      client.request<NpsReport>('GET', `/api/v1/modules/nps${buildQuery({ from, to })}`),

    createReview: (appointmentId: string, customerId: string, score: number, comment?: string) =>
      client.request<{ id: string; score: number; category: string }>(
        'POST',
        '/api/v1/modules/reviews',
        { appointmentId, customerId, score, comment },
      ),
  };
}
