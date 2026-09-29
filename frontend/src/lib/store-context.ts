import { createContext } from "react";
import type { Barber } from "./shop";
import type {
  AdminClient,
  Appointment,
  RegisterInput,
  Service,
  ServiceUpdateInput,
  User,
} from "@/services/api";

export type Ctx = {
  ready: boolean;
  catalogLoading: boolean;
  user: User | null;
  clients: AdminClient[];
  services: Service[];
  barbers: Barber[];
  appointments: Appointment[];
  signIn: (email: string, password: string) => Promise<User>;
  signUp: (data: RegisterInput) => Promise<User>;
  signOut: () => void;
  updateProfile: (data: Pick<User, "name" | "email" | "phone">) => Promise<void>;
  changePassword: (current: string, next: string) => Promise<void>;
  deleteAccount: (password: string) => Promise<void>;
  getAvailability: (
    serviceId: string,
    barberId: string,
    date: string,
    appointmentId?: string,
  ) => Promise<string[]>;
  book: (input: {
    serviceId: string;
    barberId: string;
    date: string;
    time: string;
  }) => Promise<Appointment>;
  cancelAppointment: (id: string) => Promise<void>;
  rescheduleAppointment: (id: string, date: string, time: string) => Promise<void>;
  completeAppointment: (id: string) => Promise<void>;
  removeAppointment: (id: string) => Promise<void>;
  updateService: (id: string, input: ServiceUpdateInput) => Promise<void>;
  removeService: (id: string) => Promise<void>;
};

// Mantido em módulo próprio (sem componentes) para o contexto sobreviver ao HMR.
export const StoreContext = createContext<Ctx | null>(null);
