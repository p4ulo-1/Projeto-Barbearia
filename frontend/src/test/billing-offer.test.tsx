import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, within, waitFor } from "@testing-library/react";
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
  currentCheckout: vi.fn(),
  startCheckout: vi.fn(),
  verifyCheckout: vi.fn(),
  verifySubscription: vi.fn(),
  success: vi.fn(),
}));
vi.mock("@/lib/auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user }) }));
vi.mock("@/lib/api/operations", () => ({ operations: mocks }));
vi.mock("@/components/panel/CheckoutCard", () => ({
  CheckoutCard: ({ onToken }: { onToken: (token: string) => Promise<void> }) => (
    <button onClick={() => void onToken("opaque-token")}>Autorizar cartão</button>
  ),
}));
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
    trialStartedAtUtc: "2026-11-01T12:00:00Z",
    trialEndsAtUtc: "2026-11-15T12:00:00Z",
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
  mocks.currentCheckout.mockResolvedValue(null);
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
    expect(screen.getByText(/Trial autorizado:/)).toBeInTheDocument();
    expect(screen.getByText(/seleção não altera o plano vigente/)).toBeInTheDocument();
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

describe("contratação e acompanhamento", () => {
  it("recupera cobrança programada, pendente, recusada e paga em novas consultas e sessões", async () => {
    const base = {
      planCode: "PRO", planName: "Pro", status: "AuthorizedAwaitingFirstPayment",
      periodStart: "2026-11-01", periodEnd: "2026-11-15", trialEndsAt: "2026-11-15",
    };
    mocks.subscription.mockResolvedValue({ ...base, firstPaymentStatus: "Scheduled" });
    let view = show(<BillingPage />);
    expect(await screen.findByText(/Primeira cobrança programada/)).toBeInTheDocument();
    view.unmount();

    mocks.subscription.mockResolvedValue({ ...base, firstPaymentStatus: "Pending" });
    view = show(<BillingPage />);
    expect(await screen.findByText(/Primeira cobrança aguardando confirmação/)).toBeInTheDocument();
    expect(screen.queryByText(/primeira cobrança foi recusada/i)).not.toBeInTheDocument();
    view.unmount();

    mocks.subscription.mockResolvedValue({ ...base, firstPaymentStatus: "Rejected" });
    view = show(<BillingPage />);
    expect(await screen.findByText(/primeira cobrança foi recusada/i)).toBeInTheDocument();
    view.unmount();
    // Uma nova árvore e um novo QueryClient simulam recarga e nova sessão.
    view = show(<BillingPage />);
    expect(await screen.findByText(/primeira cobrança foi recusada/i)).toBeInTheDocument();
    expect(screen.queryByText(/cobrança paga e confirmada/i)).not.toBeInTheDocument();
    view.unmount();

    mocks.subscription.mockResolvedValue({ ...base, status: "Active", firstPaymentStatus: "Paid" });
    show(<BillingPage />);
    expect(await screen.findByText(/Primeira cobrança paga e confirmada/)).toBeInTheDocument();
    expect(screen.queryByText(/primeira cobrança foi recusada/i)).not.toBeInTheDocument();
  });

  it("não inventa situação financeira se a consulta falhar", async () => {
    mocks.subscription.mockRejectedValue(new Error("Consulta indisponível"));
    show(<BillingPage />);
    expect(await screen.findByText(/Consulta indisponível/)).toBeInTheDocument();
    expect(screen.queryByText(/cobrança paga e confirmada/i)).not.toBeInTheDocument();
  });

  it("atualiza a recusa após regularização confirmada na API", async () => {
    const subscription = {
      planCode: "PRO", planName: "Pro", status: "AuthorizedAwaitingFirstPayment",
      periodStart: "2026-11-01", periodEnd: "2026-11-15", trialEndsAt: "2026-11-15",
      firstPaymentStatus: "Rejected",
    };
    mocks.subscription.mockResolvedValue(subscription);
    mocks.currentCheckout.mockResolvedValue({ checkoutKey: "key", attemptId: "attempt", state: "FirstPaymentRejected" });
    mocks.verifySubscription.mockImplementation(async () => {
      mocks.subscription.mockResolvedValue({ ...subscription, status: "Active", firstPaymentStatus: "Paid" });
      return { state: "Renewed", status: "Active" };
    });
    show(<BillingPage />);
    expect((await screen.findAllByText(/primeira cobrança foi recusada/i)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole("button", { name: "Atualizar situação financeira" }));
    expect(await screen.findByText(/Primeira cobrança paga e confirmada/)).toBeInTheDocument();
    expect(screen.queryByText(/primeira cobrança foi recusada/i)).not.toBeInTheDocument();
  });

  const attempt = {
    checkoutKey: "key-1",
    attemptId: "attempt-1",
    state: "AwaitingExternalConfirmation",
    trialStartsAt: null,
    trialEndsAt: null,
  };
  it("inicia com token opaco e aguarda confirmação sem ativar trial", async () => {
    mocks.subscription.mockResolvedValue({
      planCode: "ESSENCIAL",
      planName: "Essencial",
      status: "AwaitingTrialAuthorization",
      periodStart: "2026-11-01",
      periodEnd: "2026-11-01",
      trialEndsAt: null,
    });
    let finish!: (value: unknown) => void;
    mocks.startCheckout.mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    show(<BillingPage />);
    fireEvent.change(await screen.findByLabelText("Selecionar plano para revisão"), {
      target: { value: "PRO" },
    });
    const button = await screen.findByRole("button", { name: "Autorizar cartão" });
    fireEvent.click(button);
    fireEvent.click(button);
    await waitFor(() => expect(mocks.startCheckout).toHaveBeenCalledTimes(1));
    expect(mocks.startCheckout).toHaveBeenCalledWith(expect.any(String), "PRO", "opaque-token");
    mocks.currentCheckout.mockResolvedValue(attempt);
    finish(attempt);
    expect(await screen.findByText(/Aguardando confirmação da autorização/)).toBeInTheDocument();
    expect(screen.queryByText(/Trial confirmado:/)).not.toBeInTheDocument();
    expect(mocks.changePlan).not.toHaveBeenCalled();
  });
  it("recupera tentativa após recarga e verifica sem confiar no retorno", async () => {
    mocks.currentCheckout.mockResolvedValue({ ...attempt, state: "RequiresReconciliation" });
    mocks.verifyCheckout.mockImplementation(async () => {
      mocks.currentCheckout.mockResolvedValue(null);
      return {
        ...attempt,
        state: "Trialing",
        trialStartsAt: "2026-11-19T12:00:00Z",
        trialEndsAt: "2026-12-03T12:00:00Z",
      };
    });
    show(<BillingPage />);
    expect(await screen.findByText(/pendente de verificação/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Consultar contratação" }));
    await waitFor(() => expect(mocks.verifyCheckout).toHaveBeenCalledWith("key-1"));
    expect(await screen.findByText(/Trial confirmado:/)).toBeInTheDocument();
    expect(mocks.startCheckout).not.toHaveBeenCalled();
  });
  it("apresenta cobrança recusada sem conceder plano pago", async () => {
    mocks.currentCheckout.mockResolvedValue(attempt);
    mocks.verifyCheckout.mockResolvedValue({ ...attempt, state: "FirstPaymentRejected" });
    show(<BillingPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Consultar contratação" }));
    await waitFor(() => expect(mocks.verifyCheckout).toHaveBeenCalledWith("key-1"));
    expect(await screen.findByText(/primeira cobrança foi recusada/)).toBeInTheDocument();
    expect(mocks.changePlan).not.toHaveBeenCalled();
    expect(screen.queryByText("Pagamento confirmado e assinatura ativa.")).not.toBeInTheDocument();
  });
  it("erro de rede deixa tentativa ambígua sem nova contratação", async () => {
    mocks.subscription.mockResolvedValue({
      planCode: "ESSENCIAL",
      planName: "Essencial",
      status: "AwaitingTrialAuthorization",
      periodStart: "2026-11-01",
      periodEnd: "2026-11-01",
      trialEndsAt: null,
    });
    mocks.startCheckout.mockRejectedValue(new Error("rede"));
    show(<BillingPage />);
    fireEvent.change(await screen.findByLabelText("Selecionar plano para revisão"), {
      target: { value: "PRO" },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Autorizar cartão" }));
    expect(await screen.findByText(/Não foi possível confirmar o início/)).toBeInTheDocument();
    expect(mocks.startCheckout).toHaveBeenCalledTimes(1);
  });
  it("mostra inadimplência e cancelamento pendente conforme backend", async () => {
    mocks.subscription.mockResolvedValue({
      planCode: "PRO",
      planName: "Pro",
      status: "PastDue",
      billingAccess: "OwnerReadOnly",
      cancellationRequested: true,
      periodStart: "2026-11-01",
      periodEnd: "2026-12-01",
      trialEndsAt: null,
    });
    show(<BillingPage />);
    expect(await screen.findByText(/Novos agendamentos estão bloqueados/)).toHaveTextContent(
      "acesso está restrito ao proprietário",
    );
    expect(
      screen.getByText("Cancelamento solicitado; aguardando confirmação."),
    ).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar assinatura" })).not.toBeInTheDocument();
  });

  it("apresenta cancelamento como pendente quando ainda não confirmado", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    mocks.cancelPlan.mockResolvedValue({ state: "CancellationPending", status: "Trialing" });
    show(<BillingPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Cancelar assinatura" }));
    expect(
      await screen.findByText("Cancelamento solicitado; aguardando confirmação."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Cancelamento confirmado.")).not.toBeInTheDocument();
    expect(mocks.cancelPlan).toHaveBeenCalledTimes(1);
  });
});
