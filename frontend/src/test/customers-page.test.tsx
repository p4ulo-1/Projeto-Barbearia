import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CustomersPage } from "@/components/panel/CustomersPage";
import { ApiError } from "@/lib/api/http";

const api = vi.hoisted(() => ({ customers: vi.fn(), customer: vi.fn(), createCustomer: vi.fn(), preferences: vi.fn(), professionals: vi.fn() }));
vi.mock("@/lib/api/operations", () => ({ operations: api }));
vi.mock("@/lib/auth/AuthProvider", () => ({ useAuth: () => ({ user: { role: "owner" } }) }));

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}><CustomersPage /></QueryClientProvider>);
}

describe("clientes", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.customers.mockResolvedValue({ items: [], page: 1, totalPages: 1, hasNext: false, hasPrevious: false });
    api.professionals.mockResolvedValue({ items: [] });
  });

  it("cadastra com campos normalizados e atualiza a lista", async () => {
    api.createCustomer.mockResolvedValue({ id: "c1" });
    show();
    fireEvent.change(screen.getByLabelText("Nome"), { target: { value: " Ana " } });
    fireEvent.change(screen.getByLabelText("Telefone"), { target: { value: " 11999999999 " } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar cliente" }));
    await waitFor(() => expect(api.createCustomer).toHaveBeenCalledWith({ name: "Ana", phone: "11999999999" }));
    await waitFor(() => expect(api.customers.mock.calls.length).toBeGreaterThan(1));
  });

  it("informa quando o cliente selecionado não existe mais", async () => {
    api.customers.mockResolvedValue({ items: [{ id: "c1", name: "Ana", phone: "123", isActive: true }], page: 1, totalPages: 1 });
    api.customer.mockRejectedValue(new ApiError("Recurso não encontrado.", 404));
    show();
    fireEvent.click(await screen.findByRole("button", { name: "Ver detalhes" }));
    expect(await screen.findByText(/Cliente não encontrado ou indisponível/)).toBeInTheDocument();
    expect(api.preferences).not.toHaveBeenCalled();
  });

  it("recusa nome e telefone compostos apenas por espaços", () => {
    show();
    const name = screen.getByLabelText("Nome") as HTMLInputElement;
    const phone = screen.getByLabelText("Telefone") as HTMLInputElement;
    fireEvent.change(name, { target: { value: "   " } });
    fireEvent.change(phone, { target: { value: "   " } });
    expect(name.checkValidity()).toBe(false);
    expect(phone.checkValidity()).toBe(false);
  });

  it("apresenta erros de validação retornados pela API", async () => {
    api.createCustomer.mockRejectedValue(new ApiError("Dados inválidos", 400, { errors: { Phone: ["Telefone inválido."] } }));
    show();
    fireEvent.change(screen.getByLabelText("Nome"), { target: { value: "Ana" } });
    fireEvent.change(screen.getByLabelText("Telefone"), { target: { value: "123" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar cliente" }));
    expect(await screen.findByText("Telefone inválido.")).toBeInTheDocument();
  });
});
