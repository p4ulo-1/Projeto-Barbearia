import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { BillingPage } from "@/components/panel/BillingPage";
import { Plans } from "@/components/site/Plans";

const mocks = vi.hoisted(() => ({
  user: { role: "owner", permissions: ["ViewTenantSettings", "ManageBilling"] } as {
    role: string;
    permissions: string[];
  },
  list: vi.fn(),
  subscription: vi.fn(),
  invoices: vi.fn(),
  planHistory: vi.fn(),
  upgradeSuggestion: vi.fn(),
  changePlan: vi.fn(),
  cancelPlan: vi.fn(),
  success: vi.fn(),
}));
vi.mock("@/lib/auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user }) }));
vi.mock("@/lib/api/operations", () => ({ operations: mocks }));
vi.mock("@/lib/api/plans", async (original) => ({
  ...(await original<typeof import("@/lib/api/plans")>()),
  plansService: { list: mocks.list },
}));
vi.mock("@tanstack/react-router", () => ({
  Link: ({ children, to }: { children: React.ReactNode; to: string }) => (
    <a href={to}>{children}</a>
  ),
}));
vi.mock("sonner", () => ({ toast: { success: mocks.success, error: vi.fn() } }));
const plans = [
  {
    code: "ESSENCIAL",
    name: "Essencial",
    monthlyPrice: 111.5,
    includedProfessionals: 4,
    customizationLevel: "Basic",
    includedAiUnits: 100,
  },
  {
    code: "PRO",
    name: "Pro",
    monthlyPrice: 222.5,
    includedProfessionals: 8,
    customizationLevel: "Advanced",
    includedAiUnits: 500,
  },
];
function show(node: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{node}</QueryClientProvider>);
}
beforeEach(() => {
  vi.clearAllMocks();
  mocks.user = { role: "owner", permissions: ["ViewTenantSettings", "ManageBilling"] };
  mocks.list.mockResolvedValue(plans);
  mocks.subscription.mockResolvedValue({
    planCode: "ESSENCIAL",
    planName: "Essencial",
    status: "Trialing",
    periodStart: "2026-11-01",
    periodEnd: "2026-11-15",
    trialEndsAt: "2026-11-15",
  });
  mocks.invoices.mockResolvedValue({
    items: [],
    page: 1,
    totalPages: 1,
    hasNext: false,
    hasPrevious: false,
  });
  mocks.planHistory.mockResolvedValue([]);
  mocks.upgradeSuggestion.mockResolvedValue(null);
});

describe("oferta e seleção de planos", () => {
  it("oferece somente informações comerciais vindas da API", async () => {
    show(<Plans />);
    const card = (await screen.findByRole("heading", { name: "Pro" })).closest("article")!;
    expect(within(card).getByText(/222,50/)).toBeInTheDocument();
    expect(within(card).getByText("Até 8 profissionais elegíveis")).toBeInTheDocument();
    expect(within(card).getByText("Personalização do site: avançada")).toBeInTheDocument();
    expect(screen.queryByText(/créditos de IA/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/todos os módulos/i)).not.toBeInTheDocument();
    expect(within(card).getByRole("link", { name: "Criar conta" })).toHaveAttribute(
      "href",
      "/criar-conta",
    );
  });
  it("seleciona plano temporariamente sem alterar trial ou iniciar operação", async () => {
    show(<BillingPage />);
    const select = await screen.findByLabelText("Selecionar plano para revisão");
    fireEvent.change(select, { target: { value: "PRO" } });
    expect(screen.getByText("Opção selecionada:")).toHaveTextContent("Pro");
    expect(screen.getByText(/222,50 por mês/)).toBeInTheDocument();
    expect(screen.getByText("Até 8 profissionais elegíveis")).toBeInTheDocument();
    expect(screen.getByText("Personalização do site: avançada")).toBeInTheDocument();
    expect(screen.getByText(/Em teste/).closest("p")).toHaveTextContent("Essencial");
    expect(screen.getByText(/Fim do teste em/)).toBeInTheDocument();
    expect(screen.getByText(/seleção é temporária/)).toHaveTextContent(
      "contratação online ainda não está habilitada",
    );
    expect(
      screen.queryByRole("button", { name: /alterar plano|contratar plano/i }),
    ).not.toBeInTheDocument();
    expect(mocks.changePlan).not.toHaveBeenCalled();
    expect(mocks.cancelPlan).not.toHaveBeenCalled();
    expect(mocks.success).not.toHaveBeenCalled();
    expect(mocks.subscription).toHaveBeenCalled();
    expect(mocks.invoices).toHaveBeenCalled();
    expect(mocks.planHistory).toHaveBeenCalled();
    fireEvent.change(select, { target: { value: "ESSENCIAL" } });
    expect(screen.getByText(/\(plano atual\)/)).toBeInTheDocument();
  });
  it("respeita permissões de leitura sem oferecer escrita ou seleção", async () => {
    mocks.user = { role: "admin", permissions: ["ViewTenantSettings"] };
    show(<BillingPage />);
    expect((await screen.findByText(/Em teste/)).closest("p")).toHaveTextContent("Essencial");
    expect(screen.queryByLabelText("Selecionar plano para revisão")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar assinatura" })).not.toBeInTheDocument();
    expect(mocks.changePlan).not.toHaveBeenCalled();
  });
});
