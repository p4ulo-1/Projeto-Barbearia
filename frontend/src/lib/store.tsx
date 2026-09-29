import {
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { toast } from "sonner";
import { decorateBarbers, orderServices } from "./shop";
import { StoreContext, type Ctx } from "./store-context";
import {
  api,
  getAuthToken,
  setAuthToken,
  UNAUTHORIZED_EVENT,
  type AdminClient,
  type Appointment,
  type Service,
  type User,
} from "@/services/api";

export type { Appointment, User } from "@/services/api";

export function StoreProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [user, setUser] = useState<User | null>(null);
  const [clients, setClients] = useState<AdminClient[]>([]);
  const [services, setServices] = useState<Service[]>([]);
  const [barbers, setBarbers] = useState<ReturnType<typeof decorateBarbers>>([]);
  const [appointments, setAppointments] = useState<Appointment[]>([]);

  const clearSession = useCallback(() => {
    setAuthToken(null);
    setUser(null);
    setClients([]);
    setAppointments([]);
  }, []);

  const loadProtectedData = useCallback(async (authenticatedUser: User) => {
    if (authenticatedUser.role === "admin") {
      const [allAppointments, allClients] = await Promise.all([
        api.appointments.all(),
        api.users.clients(),
      ]);
      setAppointments(allAppointments);
      setClients(allClients);
      return;
    }

    setAppointments(await api.appointments.mine());
    setClients([]);
  }, []);

  useEffect(() => {
    let active = true;

    async function initialize() {
      const catalogsPromise = Promise.all([api.services.list(), api.barbers.list()])
        .then(([serviceList, barberList]) => {
          if (!active) return;
          setServices(orderServices(serviceList));
          setBarbers(decorateBarbers(barberList));
        })
        .catch((error: unknown) => {
          if (!active) return;
          console.error("Falha ao carregar serviços e barbeiros da API.", error);
          toast.error("Não foi possível carregar serviços e barbeiros.");
        })
        .finally(() => {
          if (active) setCatalogLoading(false);
        });

      if (getAuthToken()) {
        try {
          const currentUser = await api.users.me();
          if (active) {
            setUser(currentUser);
            await loadProtectedData(currentUser);
          }
        } catch {
          if (active) clearSession();
        }
      }

      await catalogsPromise;
      if (active) setReady(true);
    }

    void initialize();
    return () => {
      active = false;
    };
  }, [clearSession, loadProtectedData]);

  useEffect(() => {
    const handleUnauthorized = () => clearSession();
    window.addEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
  }, [clearSession]);

  const value: Ctx = useMemo(
    () => ({
      ready,
      catalogLoading,
      user,
      clients,
      services,
      barbers,
      appointments,
      async signIn(email, password) {
        const response = await api.auth.login(email, password);
        setAuthToken(response.token);
        setUser(response.user);
        try {
          await loadProtectedData(response.user);
        } catch (error) {
          clearSession();
          throw error;
        }
        return response.user;
      },
      async signUp(data) {
        const response = await api.auth.register(data);
        setAuthToken(response.token);
        setUser(response.user);
        try {
          await loadProtectedData(response.user);
        } catch (error) {
          clearSession();
          throw error;
        }
        return response.user;
      },
      signOut() {
        clearSession();
      },
      async updateProfile(data) {
        const updated = await api.users.update(data);
        setUser(updated);
      },
      async changePassword(current, next) {
        await api.users.changePassword(current, next);
      },
      async deleteAccount(password) {
        await api.users.delete(password);
        clearSession();
      },
      async getAvailability(serviceId, barberId, date, appointmentId) {
        const response = await api.appointments.availability(
          serviceId,
          barberId,
          date,
          appointmentId,
        );
        return response.slots;
      },
      async book(input) {
        const created = await api.appointments.create(input);
        setAppointments((current) => [...current, created]);
        return created;
      },
      async cancelAppointment(id) {
        await api.appointments.cancel(id);
        setAppointments((current) =>
          current.map((appointment) =>
            appointment.id === id
              ? { ...appointment, status: "cancelado" as const }
              : appointment,
          ),
        );
      },
      async rescheduleAppointment(id, date, time) {
        const updated = await api.appointments.reschedule(id, date, time);
        setAppointments((current) =>
          current.map((appointment) => (appointment.id === id ? updated : appointment)),
        );
      },
      async completeAppointment(id) {
        await api.appointments.complete(id);
        setAppointments((current) =>
          current.map((appointment) =>
            appointment.id === id
              ? { ...appointment, status: "concluido" as const }
              : appointment,
          ),
        );
      },
      async removeAppointment(id) {
        await api.appointments.delete(id);
        setAppointments((current) =>
          current.filter((appointment) => appointment.id !== id),
        );
        setClients((current) =>
          current.map((client) =>
            client.id === appointments.find((appointment) => appointment.id === id)?.userId
              ? { ...client, appointmentCount: Math.max(0, client.appointmentCount - 1) }
              : client,
          ),
        );
      },
      async updateService(id, input) {
        const updated = await api.services.update(id, input);
        setServices((current) =>
          orderServices(
            current.map((service) => (service.id === id ? updated : service)),
          ),
        );
      },
      async removeService(id) {
        await api.services.delete(id);
        setServices((current) => current.filter((service) => service.id !== id));
      },
    }),
    [
      appointments,
      barbers,
      catalogLoading,
      clearSession,
      clients,
      loadProtectedData,
      ready,
      services,
      user,
    ],
  );

  return <StoreContext.Provider value={value}>{children}</StoreContext.Provider>;
}

export function useStore() {
  const ctx = useContext(StoreContext);
  if (!ctx) throw new Error("useStore precisa estar dentro de StoreProvider");
  return ctx;
}

export function barberWorksOn(barber: ReturnType<typeof decorateBarbers>[number], date: string) {
  const day = new Date(`${date}T12:00:00`).getDay();
  return barber.workdays.includes(day);
}
