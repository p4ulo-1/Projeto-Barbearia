import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AgendaPage } from "@/components/panel/AgendaPage";

const mocks = vi.hoisted(() => ({
  user: { role: "owner" } as { role: string },
  api: {
    appointments: vi.fn(), ownAppointments: vi.fn(), ownProfessional: vi.fn(),
    professionals: vi.fn(), services: vi.fn(), professionalServices: vi.fn(),
    customers: vi.fn(), availability: vi.fn(), ownAvailability: vi.fn(),
    rescheduleAvailability: vi.fn(),
  },
}));

vi.mock("@/lib/auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user }) }));
vi.mock("@/lib/api/operations", () => ({ operations: mocks.api }));
vi.mock("@tanstack/react-router", async (original) => ({
  ...(await original<typeof import("@tanstack/react-router")>()),
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><AgendaPage /></QueryClientProvider>);
}

describe("agenda da Fase 1", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.user = { role: "owner" };
    mocks.api.appointments.mockResolvedValue([]);
    mocks.api.ownAppointments.mockResolvedValue([]);
    mocks.api.ownProfessional.mockResolvedValue({ professionalId: "own-professional" });
    mocks.api.professionals.mockResolvedValue({ items: [
      { id: "eligible", name: "Pessoa com serviço", isActive: true, isServiceProvider: false },
    ] });
    mocks.api.services.mockResolvedValue({ items: [
      { id: "service", name: "Corte", isActive: true, durationMinutes: 30 },
    ] });
    mocks.api.professionalServices.mockResolvedValue(["service"]);
    mocks.api.customers.mockResolvedValue({ items: [] });
    mocks.api.availability.mockResolvedValue({ availableSlots: ["09:00:00"] });
    mocks.api.ownAvailability.mockResolvedValue({ availableSlots: ["09:00:00"] });
    mocks.api.rescheduleAvailability.mockResolvedValue({ availableSlots: ["09:00:00"] });
  });

  it("oferece profissionais ativos mesmo sem o marcador legado isServiceProvider", async () => {
    show();
    const select = await screen.findByLabelText("Profissional");
    expect(within(select).getByRole("option", { name: "Pessoa com serviço" })).toBeInTheDocument();
    fireEvent.change(select, { target: { value: "eligible" } });
    expect(await screen.findByRole("option", { name: "Corte" })).toBeInTheDocument();
  });

  it("usa o vínculo do servidor e consulta apenas a agenda própria", async () => {
    mocks.user = { role: "professional" };
    show();
    expect(await screen.findByText("Novo atendimento na minha agenda")).toBeInTheDocument();
    await waitFor(() => expect(mocks.api.ownAppointments).toHaveBeenCalled());
    expect(mocks.api.appointments).not.toHaveBeenCalled();
    expect(mocks.api.professionals).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText("Serviço"), { target: { value: "service" } });
    await waitFor(() => expect(mocks.api.ownAvailability).toHaveBeenCalledWith(
      expect.any(String), "service",
    ));
    expect(mocks.api.availability).not.toHaveBeenCalled();
  });

  it("consulta horários de remarcação pelo compromisso persistido", async () => {
    mocks.api.appointments.mockResolvedValue([{
      id: "appointment", customerId: "customer", serviceId: "service",
      professionalId: "eligible", date: "2026-11-23", time: "09:00:00",
      durationMinutes: 30, status: "Confirmed",
    }]);
    show();
    fireEvent.click(await screen.findByRole("button", { name: "Remarcar" }));
    await waitFor(() => expect(mocks.api.rescheduleAvailability).toHaveBeenCalledWith(
      "appointment", "2026-11-23",
    ));
    expect(mocks.api.availability).not.toHaveBeenCalled();
  });
});
