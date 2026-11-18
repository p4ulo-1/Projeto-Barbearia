export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface Session {
  id: string;
  businessName: string;
  segment: string | number;
  status: "Collecting" | "Reviewing" | "AwaitingApproval" | "Approved" | "Published";
  messageCount: number;
  generatedChangeSetId: string | null;
  startedAt: string;
}

export interface Section {
  componentType: string;
  configuration: Record<string, string>;
  sortOrder?: number;
  order?: number;
}

export interface Configuration {
  status: Session["status"];
  summary: string;
  theme: unknown;
  sections: Section[];
  note?: string | null;
}

export interface Asset {
  id: string;
  fileName: string;
  contentType: string;
  sizeInBytes: number;
  kind: string;
  status: "Pending" | "Approved" | "Rejected";
  isUsable: boolean;
}

export const onboardingPath = "/api/v1/onboarding";
export const sessionPath = (id: string) => `${onboardingPath}/sessions/${encodeURIComponent(id)}`;
export const slugPattern = "[a-z0-9][a-z0-9\\-]{1,38}[a-z0-9]";
export const tenantPattern = "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}";
