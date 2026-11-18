import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { FinancePage } from "@/components/panel/FinancePage";
import { ApiError } from "@/lib/api/http";

const mocks = vi.hoisted(() => ({
  user: { role: "owner" },
  api: { report: vi.fn(), transactions: vi.fn(), transaction: vi.fn(), payment: vi.fn(), refund: vi.fn(), professionals: vi.fn(), commissions: vi.fn() },
  error: vi.fn(), success: vi.fn(),
}));
vi.mock("@/lib/api/operations", () => ({ operations: mocks.api }));
vi.mock("@/lib/auth/AuthProvider", () => ({ useAuth: () => ({ user: mocks.user }) }));
vi.mock("sonner", () => ({ toast: { error: mocks.error, success: mocks.success } }));

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(<QueryClientProvider client={client}><FinancePage /></QueryClientProvider>);
}

describe("financeiro mínimo", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.user = { role: "owner" };
    mocks.api.report.mockResolvedValue({ totalIncome: 100, totalExpense: 20, balance: 80, averageTicket: 50 });
    mocks.api.transactions.mockResolvedValue({ items: [{ id: "t1", description: "Venda", type: "Income", amount: 100 }], page: 1, totalPages: 1, hasNext: false, hasPrevious: false });
    mocks.api.professionals.mockResolvedValue({ items: [] });
  });

  it("apresenta relatório e movimentações consultados", async () => {
    show();
    const row = (await screen.findByText("Venda")).closest("tr")!;
    expect(within(row).getByText("Receita")).toBeInTheDocument();
    expect(within(row).getByText(/100,00/)).toBeInTheDocument();
    expect(screen.getByText("Saldo")).toBeInTheDocument();
    expect(mocks.api.transactions).toHaveBeenCalledWith(1, expect.any(String), expect.any(String));
  });

  it("mostra ausência e erros de consulta", async () => {
    mocks.api.transactions.mockResolvedValue({ items: [] });
    mocks.api.report.mockRejectedValue(new ApiError("Sem permissão", 403));
    show();
    expect(await screen.findByText("Nenhuma movimentação no período.")).toBeInTheDocument();
    expect(await screen.findByText(/Sem permissão/)).toBeInTheDocument();
  });

  it("valida descrição e impede envio repetido enquanto registra e atualiza a consulta", async () => {
    let finish!: (value: unknown) => void;
    mocks.api.transaction.mockReturnValue(new Promise((resolve) => { finish = resolve; }));
    show();
    await screen.findByText("Venda");
    const form = screen.getByRole("button", { name: "Registrar movimentação" }).closest("form")!;
    const description = within(form).getByLabelText("Descrição") as HTMLInputElement;
    fireEvent.change(description, { target: { value: "   " } });
    expect(description.checkValidity()).toBe(false);
    fireEvent.change(description, { target: { value: "  Produto  " } });
    fireEvent.change(within(form).getByLabelText("Tipo"), { target: { value: "2" } });
    fireEvent.change(within(form).getByLabelText("Valor (R$)"), { target: { value: "25" } });
    fireEvent.submit(form);
    fireEvent.submit(form);
    await waitFor(() => expect(mocks.api.transaction).toHaveBeenCalledTimes(1));
    expect(mocks.api.transaction).toHaveBeenCalledWith({ type: 2, amount: 25, description: "Produto" });
    finish({ id: "t2" });
    await waitFor(() => expect(mocks.api.transactions.mock.calls.length).toBeGreaterThan(1));
  });

  it("apresenta erro da API ao lançar", async () => {
    mocks.api.transaction.mockRejectedValue(new ApiError("Valor recusado", 400, { errors: { Amount: ["Valor inválido."] } }));
    show();
    const form = screen.getByRole("button", { name: "Registrar movimentação" }).closest("form")!;
    fireEvent.change(within(form).getByLabelText("Tipo"), { target: { value: "1" } });
    fireEvent.change(within(form).getByLabelText("Valor (R$)"), { target: { value: "10" } });
    fireEvent.change(within(form).getByLabelText("Descrição"), { target: { value: "Venda" } });
    fireEvent.submit(form);
    expect(await screen.findByText("Valor inválido.")).toBeInTheDocument();
  });

  it("registra pagamento presencial e apresenta dados retornados para estorno", async () => {
    mocks.api.payment.mockResolvedValue({ id: "12345678-1234-1234-1234-123456789012", amount: 50, method: "Pix", status: "Paid" });
    show();
    const form = screen.getByRole("button", { name: "Registrar pagamento" }).closest("form")!;
    fireEvent.change(within(form).getByLabelText("Valor (R$)"), { target: { value: "50" } });
    fireEvent.change(within(form).getByLabelText("Forma de pagamento"), { target: { value: "4" } });
    fireEvent.submit(form);
    await waitFor(() => expect(mocks.api.payment).toHaveBeenCalledWith(50, 4, undefined));
    expect(await screen.findByText(/Pagamento:.*Pix.*Pago/)).toBeInTheDocument();
    expect(screen.getByLabelText("ID do pagamento")).toHaveValue("12345678-1234-1234-1234-123456789012");
  });

  it("envia o estorno confirmado e apresenta o estado retornado", async () => {
    const id = "12345678-1234-1234-1234-123456789012";
    mocks.api.refund.mockResolvedValue({ id, amount: 50, method: "Cash", status: "PartiallyRefunded" });
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    show();
    const form = screen.getByRole("button", { name: "Confirmar estorno" }).closest("form")!;
    fireEvent.change(within(form).getByLabelText("ID do pagamento"), { target: { value: id } });
    fireEvent.change(within(form).getByLabelText("Valor do estorno (R$)"), { target: { value: "10" } });
    fireEvent.submit(form);
    await waitFor(() => expect(mocks.api.refund).toHaveBeenCalledWith(id, 10));
    expect(await screen.findByText(/Parcialmente estornado/)).toBeInTheDocument();
    confirm.mockRestore();
  });
});
