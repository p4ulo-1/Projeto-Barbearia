/**
 * Tipos compartilhados com a API (F14.1).
 *
 * Espelham os contratos do backend. Quando um contrato muda, o TypeScript
 * acusa o erro em tempo de compilação, não em produção.
 */

export interface Service {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly price: number;
  readonly durationMinutes: number;
  readonly categoryId: string | null;
  readonly highlight: boolean;
  readonly isActive: boolean;
}

export interface Category {
  readonly id: string;
  readonly name: string;
  readonly sortOrder: number;
}

export interface Professional {
  readonly id: string;
  readonly name: string;
  readonly title: string | null;
  readonly isActive: boolean;
}

export interface Customer {
  readonly id: string;
  readonly name: string;
  readonly phone: string;
  readonly email: string | null;
  readonly isActive: boolean;
}

export interface Appointment {
  readonly id: string;
  readonly tenantId: string;
  readonly customerId: string;
  readonly serviceId: string;
  readonly professionalId: string;
  readonly date: string;
  readonly time: string;
  readonly durationMinutes: number;
  readonly status: string;
  readonly cancellationReason: string | null;
  readonly createdAt: string;
}

export interface Availability {
  readonly professionalId: string;
  readonly date: string;
  readonly availableSlots: readonly string[];
}

export interface Payment {
  readonly id: string;
  readonly amount: number;
  readonly method: string;
  readonly status: string;
}

export interface FinancialReport {
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly totalIncome: number;
  readonly totalExpense: number;
  readonly balance: number;
  readonly averageTicket: number;
  readonly appointmentCount: number;
}

export interface Subscription {
  readonly id: string;
  readonly planCode: string;
  readonly planName: string;
  readonly status: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly trialEndsAt: string | null;
}

export interface Plan {
  readonly code: string;
  readonly name: string;
  readonly monthlyPrice: number;
  readonly includedProfessionals: number;
  readonly customizationLevel: string;
  readonly includedAiUnits: number;
}

export interface Site {
  readonly id: string;
  readonly subdomain: string;
  readonly status: string;
  readonly isPublished: boolean;
  readonly pageCount: number;
  readonly publicationCount: number;
}

export interface Conversation {
  readonly id: string;
  readonly phoneNumber: string;
  readonly customerId: string | null;
  readonly status: string;
  readonly messageCount: number;
}

export interface LoyaltyAccount {
  readonly customerId: string;
  readonly points: number;
  readonly totalEarned: number;
  readonly totalRedeemed: number;
}

export interface NpsReport {
  readonly totalResponses: number;
  readonly promoters: number;
  readonly passives: number;
  readonly detractors: number;
  readonly score: number;
}
