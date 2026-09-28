import { createContext } from "react";

export type User = {
  id: string;
  name: string;
  email: string;
  phone: string;
  password: string;
  role: "client" | "admin";
};

export type Appointment = {
  id: string;
  userId: string;
  userName: string;
  serviceId: string;
  barberId: string;
  date: string; // yyyy-mm-dd
  time: string; // HH:mm
  status: "confirmado" | "cancelado" | "concluido";
  createdAt: string;
};

export type Ctx = {
  ready: boolean;
  user: User | null;
  users: User[];
  appointments: Appointment[];
  signIn: (email: string, password: string) => Promise<User>;
  signUp: (data: Omit<User, "id" | "role">) => Promise<User>;
  signOut: () => void;
  updateProfile: (data: Pick<User, "name" | "email" | "phone">) => Promise<void>;
  changePassword: (current: string, next: string) => Promise<void>;
  deleteAccount: (password: string) => Promise<void>;
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
};

// Mantido em módulo próprio (sem componentes) para o contexto sobreviver ao HMR.
export const StoreContext = createContext<Ctx | null>(null);
