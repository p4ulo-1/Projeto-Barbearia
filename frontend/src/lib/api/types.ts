// DTO contracts expected from the Servia backend. Align names with the .NET DTOs on integration.

export type OnboardingStatus =
  "in_progress" | "generating" | "ready" | "publishing" | "published" | "failed";

export interface UserDto {
  tenantId?: string;
  id: string;
  name: string;
  email: string;
  establishmentName: string;
  segment: string;
  onboardingStatus: OnboardingStatus;
  billingSetupRequired?: boolean;
  /** Access profile defined by the backend. The API remains the final authority. */
  role?: UserRole | undefined;
  permissions?: string[] | undefined;
}

export type UserRole =
  "owner" | "admin" | "manager" | "professional" | "receptionist" | "customer" | "platform";

export interface AuthResponseDto {
  accessToken: string;
  expiresAt?: string | undefined;
  user: UserDto;
}

export interface RegisterRequestDto {
  slug?: string;
  establishmentName: string;
  address: string;
  segment: string;
  document?: string | undefined;
  ownerName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface LoginRequestDto {
  tenantId?: string;
  email: string;
  password: string;
}

export interface ChatMessageDto {
  id: string;
  role: "assistant" | "user";
  content: string;
  createdAt: string;
  attachmentIds?: string[] | undefined;
}

export interface UploadedFileDto {
  id: string;
  name: string;
  contentType: string;
  size: number;
  url?: string | undefined;
}

export interface CollectedInfoDto {
  key: string;
  label: string;
  value: string;
}

export interface OnboardingSessionDto {
  id: string;
  status: OnboardingStatus;
  messages: ChatMessageDto[];
  files: UploadedFileDto[];
  collected: CollectedInfoDto[];
  canGenerate: boolean;
  progress: { current: number; total: number; label: string };
  /** Upload constraints defined by the backend. */
  uploadRules: { accept: string[]; maxSizeBytes: number; maxFiles: number };
  publicUrl?: string | undefined;
  publishedAt?: string | undefined;
  failureReason?: string | undefined;
}

/** Structured site configuration produced by the backend (never raw code). */
export interface SiteConfigDto {
  templateId: string;
  establishment: { name: string; segment: string; address?: string };
  sections: { key: string; title: string; content: string }[];
  theme: { primary?: string | undefined; styleNotes?: string | undefined };
  logoUrl?: string | undefined;
  gallery: string[];
}

export interface AppointmentSummaryDto {
  id: string;
  startsAt: string;
  clientName: string;
  serviceName: string;
  professionalName: string;
  status: "scheduled" | "confirmed" | "in_progress" | "completed" | "cancelled" | "no_show";
}

/** Dashboard overview. Any indicator the backend cannot provide comes back as null. */
export interface DashboardDto {
  isNewEstablishment: boolean;
  todayAppointments: number | null;
  clientsCount: number | null;
  revenue: { amount: number; currency: string; periodLabel: string } | null;
  topServices: { name: string; count: number }[] | null;
  upcoming: AppointmentSummaryDto[] | null;
  site: {
    status: OnboardingStatus | null;
    publicUrl?: string | undefined;
    publishedAt?: string | undefined;
  };
  subscription: { planName: string; statusLabel: string } | null;
}
