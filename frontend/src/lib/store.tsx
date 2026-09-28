import {
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { BARBERS, SERVICES, serviceById, type Barber } from "./shop";
import { StoreContext, type Ctx, type Appointment, type User } from "./store-context";

export type { Appointment, User };

/**
 * Camada de dados temporária (somente frontend).
 * Persiste em localStorage e simula latência de rede.
 * Quando o backend for ligado, basta trocar as funções deste arquivo.
 */


const USERS_KEY = "cn.users";
const SESSION_KEY = "cn.session";
const APPTS_KEY = "cn.appointments";

export const delay = (ms = 650) => new Promise((r) => setTimeout(r, ms));

const uid = () => Math.random().toString(36).slice(2, 10);

function read<T>(key: string, fallback: T): T {
  if (typeof window === "undefined") return fallback;
  try {
    const raw = window.localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function write(key: string, value: unknown) {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(key, JSON.stringify(value));
}

const SEED_USERS: User[] = [
  {
    id: "u-admin",
    name: "Rafael Nunes",
    email: "admin@newagebarber.com.br",
    phone: "(21) 99999-0001",
    password: "newage123",
    role: "admin",
  },
  {
    id: "u-demo",
    name: "Bruno Salgado",
    email: "cliente@exemplo.com",
    phone: "(21) 98888-1234",
    password: "cliente123",
    role: "client",
  },
];

function isoDay(offset: number) {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return d.toISOString().slice(0, 10);
}

const SEED_APPTS: Appointment[] = [
  {
    id: "a-1",
    userId: "u-demo",
    userName: "Bruno Salgado",
    serviceId: "combo-premium",
    barberId: "rafael",
    date: isoDay(2),
    time: "15:00",
    status: "confirmado",
    createdAt: new Date().toISOString(),
  },
  {
    id: "a-2",
    userId: "u-demo",
    userName: "Bruno Salgado",
    serviceId: "barba-terapia",
    barberId: "tiago",
    date: isoDay(-9),
    time: "11:30",
    status: "concluido",
    createdAt: new Date().toISOString(),
  },
  {
    id: "a-3",
    userId: "u-x1",
    userName: "Diego Ferrari",
    serviceId: "corte-navalhado",
    barberId: "helena",
    date: isoDay(1),
    time: "10:00",
    status: "confirmado",
    createdAt: new Date().toISOString(),
  },
];


export function StoreProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [users, setUsers] = useState<User[]>(SEED_USERS);
  const [appointments, setAppointments] = useState<Appointment[]>(SEED_APPTS);
  const [userId, setUserId] = useState<string | null>(null);

  useEffect(() => {
    setUsers(read(USERS_KEY, SEED_USERS));
    setAppointments(read(APPTS_KEY, SEED_APPTS));
    setUserId(read<string | null>(SESSION_KEY, null));
    setReady(true);
  }, []);

  const persistUsers = useCallback((next: User[]) => {
    setUsers(next);
    write(USERS_KEY, next);
  }, []);

  const persistAppts = useCallback((next: Appointment[]) => {
    setAppointments(next);
    write(APPTS_KEY, next);
  }, []);

  const user = useMemo(
    () => users.find((u) => u.id === userId) ?? null,
    [users, userId],
  );

  const value: Ctx = useMemo(
    () => ({
      ready,
      user,
      users,
      appointments,
      async signIn(email, password) {
        await delay();
        const found = users.find(
          (u) => u.email.toLowerCase() === email.trim().toLowerCase(),
        );
        if (!found || found.password !== password) {
          throw new Error("E-mail ou senha incorretos.");
        }
        setUserId(found.id);
        write(SESSION_KEY, found.id);
        return found;
      },
      async signUp(data) {
        await delay();
        if (users.some((u) => u.email.toLowerCase() === data.email.toLowerCase())) {
          throw new Error("Já existe uma conta com este e-mail.");
        }
        const created: User = { ...data, id: uid(), role: "client" };
        persistUsers([...users, created]);
        setUserId(created.id);
        write(SESSION_KEY, created.id);
        return created;
      },
      signOut() {
        setUserId(null);
        write(SESSION_KEY, null);
      },
      async updateProfile(data) {
        await delay();
        if (!user) throw new Error("Sessão expirada.");
        persistUsers(users.map((u) => (u.id === user.id ? { ...u, ...data } : u)));
      },
      async changePassword(current, next) {
        await delay();
        if (!user) throw new Error("Sessão expirada.");
        if (user.password !== current) throw new Error("Senha atual incorreta.");
        if (next.length < 6) throw new Error("A nova senha precisa ter ao menos 6 caracteres.");
        persistUsers(users.map((u) => (u.id === user.id ? { ...u, password: next } : u)));
      },
      async deleteAccount(password) {
        await delay();
        if (!user) throw new Error("Sessão expirada.");
        if (user.password !== password) throw new Error("Senha incorreta.");
        persistUsers(users.filter((u) => u.id !== user.id));
        persistAppts(appointments.filter((a) => a.userId !== user.id));
        setUserId(null);
        write(SESSION_KEY, null);
      },
      async book({ serviceId, barberId, date, time }) {
        await delay(900);
        if (!user) throw new Error("Faça login para concluir o agendamento.");
        if (!serviceById(serviceId)) throw new Error("Serviço indisponível.");
        const clash = appointments.some(
          (a) =>
            a.status === "confirmado" &&
            a.barberId === barberId &&
            a.date === date &&
            a.time === time,
        );
        if (clash) throw new Error("Este horário acabou de ser reservado. Escolha outro.");
        const created: Appointment = {
          id: uid(),
          userId: user.id,
          userName: user.name,
          serviceId,
          barberId,
          date,
          time,
          status: "confirmado",
          createdAt: new Date().toISOString(),
        };
        persistAppts([...appointments, created]);
        return created;
      },
      async cancelAppointment(id) {
        await delay(500);
        persistAppts(
          appointments.map((a) =>
            a.id === id ? { ...a, status: "cancelado" as const } : a,
          ),
        );
      },
      async rescheduleAppointment(id, date, time) {
        await delay(600);
        persistAppts(
          appointments.map((a) =>
            a.id === id ? { ...a, date, time, status: "confirmado" as const } : a,
          ),
        );
      },
      async completeAppointment(id) {
        await delay(400);
        persistAppts(
          appointments.map((a) =>
            a.id === id ? { ...a, status: "concluido" as const } : a,
          ),
        );
      },
      async removeAppointment(id) {
        await delay(400);
        persistAppts(appointments.filter((a) => a.id !== id));
      },
    }),
    [ready, user, users, appointments, persistUsers, persistAppts],
  );

  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}

export function useStore() {
  const ctx = useContext(StoreContext);
  if (!ctx) throw new Error("useStore precisa estar dentro de StoreProvider");
  return ctx;
}

/* ---------- Grade de horários por duração do serviço ---------- */

const toMinutes = (hhmm: string) => {
  const [h = 0, m = 0] = hhmm.split(":").map(Number);
  return h * 60 + m;
};
const toHHMM = (mins: number) =>
  `${String(Math.floor(mins / 60)).padStart(2, "0")}:${String(mins % 60).padStart(2, "0")}`;

export function barberWorksOn(barber: Barber, date: string) {
  const day = new Date(`${date}T12:00:00`).getDay();
  return barber.workdays.includes(day);
}

export function buildSlots(
  barber: Barber,
  date: string,
  durationMin: number,
  taken: Appointment[],
) {
  if (!barberWorksOn(barber, date)) return [];
  const step = 30;
  const start = toMinutes(barber.start);
  const end = toMinutes(barber.end);
  const busy = taken
    .filter((a) => a.barberId === barber.id && a.date === date && a.status === "confirmado")
    .map((a) => {
      const s = toMinutes(a.time);
      const dur = SERVICES.find((sv) => sv.id === a.serviceId)?.duration ?? 30;
      return [s, s + dur] as const;
    });

  const now = new Date();
  const isToday = date === now.toISOString().slice(0, 10);
  const nowMin = now.getHours() * 60 + now.getMinutes();

  const slots: string[] = [];
  for (let t = start; t + durationMin <= end; t += step) {
    if (isToday && t <= nowMin + 30) continue;
    const overlaps = busy.some(([bs, be]) => t < be && t + durationMin > bs);
    if (!overlaps) slots.push(toHHMM(t));
  }
  return slots;
}

export const ALL_BARBERS = BARBERS;