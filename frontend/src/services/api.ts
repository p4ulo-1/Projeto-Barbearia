export type User = {
  id: string;
  name: string;
  email: string;
  phone: string;
  role: "client" | "admin";
};

export type AuthResponse = {
  token: string;
  expiresAt: string;
  user: User;
};

export type RegisterInput = {
  name: string;
  email: string;
  phone: string;
  password: string;
};

export type Service = {
  id: string;
  name: string;
  description: string;
  price: number;
  duration: number;
  highlight: boolean;
};

export type ServiceUpdateInput = Omit<Service, "id">;

export type ApiBarber = {
  id: string;
  name: string;
  role: string;
  bio: string;
  workDays: number[];
  start: string;
  end: string;
};

export type Appointment = {
  id: string;
  userId: string;
  userName: string;
  serviceId: string;
  serviceName: string;
  barberId: string;
  barberName: string;
  date: string;
  time: string;
  status: "confirmado" | "cancelado" | "concluido";
  createdAt: string;
};

export type AdminClient = {
  id: string;
  name: string;
  email: string;
  phone: string;
  appointmentCount: number;
};

export type AvailabilityResponse = {
  date: string;
  slots: string[];
};

type ErrorResponse = {
  message?: string;
  errors?: Record<string, string[]>;
};

const API_URL = (import.meta.env.VITE_API_URL || "http://localhost:5071/api").replace(/\/+$/, "");
const TOKEN_KEY = "newage.auth.token";

export const UNAUTHORIZED_EVENT = "newage:unauthorized";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly validationErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function getAuthToken() {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_KEY);
}

export function setAuthToken(token: string | null) {
  if (typeof window === "undefined") return;
  if (token) window.localStorage.setItem(TOKEN_KEY, token);
  else window.localStorage.removeItem(TOKEN_KEY);
}

function statusMessage(status: number) {
  const messages: Record<number, string> = {
    400: "Os dados enviados são inválidos.",
    401: "Sua sessão expirou. Entre novamente.",
    403: "Você não tem permissão para realizar esta ação.",
    404: "O recurso solicitado não foi encontrado.",
    409: "A operação entrou em conflito com dados já existentes.",
    500: "O servidor encontrou um erro inesperado.",
  };
  return messages[status] ?? "Não foi possível concluir a solicitação.";
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = getAuthToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, { ...options, headers });
  } catch {
    throw new ApiError("Não foi possível conectar ao servidor.", 0);
  }

  if (response.status === 204) return undefined as T;

  const isJson = response.headers.get("content-type")?.includes("application/json");
  const data = isJson ? ((await response.json()) as unknown) : null;

  if (!response.ok) {
    const error = data && typeof data === "object" ? (data as ErrorResponse) : null;
    const message = error?.message || statusMessage(response.status);

    if (response.status === 401) {
      setAuthToken(null);
      if (typeof window !== "undefined") {
        window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
      }
    }

    throw new ApiError(message, response.status, error?.errors);
  }

  return data as T;
}

const json = (value: unknown) => JSON.stringify(value);

export const api = {
  auth: {
    login: (email: string, password: string) =>
      request<AuthResponse>("/auth/login", {
        method: "POST",
        body: json({ email, password }),
      }),
    register: (input: RegisterInput) =>
      request<AuthResponse>("/auth/register", {
        method: "POST",
        body: json(input),
      }),
  },
  users: {
    me: () => request<User>("/users/me"),
    update: (input: Pick<User, "name" | "email" | "phone">) =>
      request<User>("/users/me", { method: "PUT", body: json(input) }),
    changePassword: (currentPassword: string, newPassword: string) =>
      request<void>("/users/me/password", {
        method: "PUT",
        body: json({ currentPassword, newPassword }),
      }),
    delete: (password: string) =>
      request<void>("/users/me", { method: "DELETE", body: json({ password }) }),
    clients: () => request<AdminClient[]>("/users/clients"),
  },
  services: {
    list: () => request<Service[]>("/services"),
    update: (id: string, input: ServiceUpdateInput) =>
      request<Service>(`/services/${encodeURIComponent(id)}`, {
        method: "PUT",
        body: json(input),
      }),
    delete: (id: string) =>
      request<void>(`/services/${encodeURIComponent(id)}`, { method: "DELETE" }),
  },
  barbers: {
    list: () => request<ApiBarber[]>("/barbers"),
  },
  appointments: {
    mine: () => request<Appointment[]>("/appointments/me"),
    all: () => request<Appointment[]>("/appointments"),
    availability: (serviceId: string, barberId: string, date: string, appointmentId?: string) => {
      const query = new URLSearchParams({ serviceId, barberId, date });
      if (appointmentId) query.set("appointmentId", appointmentId);
      return request<AvailabilityResponse>(`/appointments/availability?${query}`);
    },
    create: (input: { serviceId: string; barberId: string; date: string; time: string }) =>
      request<Appointment>("/appointments", { method: "POST", body: json(input) }),
    reschedule: (id: string, date: string, time: string) =>
      request<Appointment>(`/appointments/${encodeURIComponent(id)}/reschedule`, {
        method: "PUT",
        body: json({ date, time }),
      }),
    cancel: (id: string) =>
      request<void>(`/appointments/${encodeURIComponent(id)}/cancel`, {
        method: "PATCH",
      }),
    complete: (id: string) =>
      request<void>(`/appointments/${encodeURIComponent(id)}/complete`, {
        method: "PATCH",
      }),
    delete: (id: string) =>
      request<void>(`/appointments/${encodeURIComponent(id)}`, { method: "DELETE" }),
  },
};
