import type { PublicAppointment, PublicAvailability, PublicProfessional, PublicService } from './types';

export interface PublicSite {
  readonly businessName: string;
  readonly subdomain: string;
}
export interface PublicBooking {
  readonly serviceId: string;
  readonly professionalId: string;
  readonly date: string;
  readonly time: string;
  readonly name: string;
  readonly phone: string;
  readonly email: string | null;
}
export class PublicApiError extends Error {
  constructor(readonly status: number) {
    super(status === 404 ? 'Estabelecimento ou recurso não disponível.' : 'Não foi possível carregar os dados.');
  }
}
/** O host é o endereço público do site, nunca um tenantId ou token administrativo. */
export function publicContext(search = window.location.search, hostname = window.location.hostname): string {
  return new URLSearchParams(search).get('host')?.trim() || hostname;
}
export function bookingPath(host: string, serviceId?: string): string {
  const query = new URLSearchParams({ host });
  if (serviceId) query.set('serviceId', serviceId);
  return `/agendar?${query.toString()}`;
}
export function createPublicApi(baseUrl: string, host: string) {
  async function request<T>(path: string, query: Record<string, string> = {}, init?: RequestInit): Promise<T> {
    const params = new URLSearchParams({ ...query, host });
    const response = await fetch(`${baseUrl}/api/v1/public/site${path}?${params.toString()}`, {
      ...init, credentials: 'omit',
    });
    if (!response.ok) throw new PublicApiError(response.status);
    return await response.json() as T;
  }
  return {
    site: () => request<PublicSite>(''),
    services: () => request<readonly PublicService[]>('/services'),
    professionals: (serviceId: string) => request<readonly PublicProfessional[]>('/professionals', { serviceId }),
    availability: (serviceId: string, professionalId: string, date: string) =>
      request<PublicAvailability>('/availability', { serviceId, professionalId, date }),
    book: (body: PublicBooking) => request<PublicAppointment>('/appointments', {}, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }),
  };
}
