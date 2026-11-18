import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CatalogPage } from "@/components/panel/CatalogPage";
import { TeamPage } from "@/components/panel/TeamPage";
import { ApiError } from "@/lib/api/http";
import { toast } from "sonner";

const state = vi.hoisted(() => ({
  permissions: ["ViewServices", "ManageServices", "ViewTeamAgenda", "ManageTeam"] as string[],
}));
const api = vi.hoisted(() => ({
  services: vi.fn(),
  categories: vi.fn(),
  combos: vi.fn(),
  service: vi.fn(),
  createService: vi.fn(),
  updateService: vi.fn(),
  price: vi.fn(),
  deactivate: vi.fn(),
  activateService: vi.fn(),
  professionals: vi.fn(),
  professional: vi.fn(),
  createPro: vi.fn(),
  updatePro: vi.fn(),
  activatePro: vi.fn(),
  deactivatePro: vi.fn(),
  setProvider: vi.fn(),
  professionalServices: vi.fn(),
  assignService: vi.fn(),
  unassignService: vi.fn(),
  blocks: vi.fn(),
  roles: vi.fn(),
  assignable: vi.fn(),
}));
vi.mock("@/lib/api/operations", () => ({
  operations: api,
  daysOfWeek: ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
}));
vi.mock("@/lib/auth/AuthProvider", () => ({
  useAuth: () => ({ user: { role: "owner", permissions: state.permissions } }),
}));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
const service = {
  id: "s1",
  name: "Corte",
  price: 20,
  durationMinutes: 30,
  isActive: true,
  highlight: false,
  description: "",
  categoryId: null,
};
const person = {
  id: "p1",
  name: "Ana",
  title: "Barbeira",
  bio: "Bio",
  isActive: true,
  isServiceProvider: true,
};
const page = (items: unknown[]) => ({
  items,
  page: 1,
  totalPages: 1,
  hasNext: false,
  hasPrevious: false,
});
function show(node: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={client}>{node}</QueryClientProvider>);
}
beforeEach(() => {
  vi.resetAllMocks();
  state.permissions = ["ViewServices", "ManageServices", "ViewTeamAgenda", "ManageTeam"];
  api.services.mockResolvedValue(page([service]));
  api.categories.mockResolvedValue([{ id: "c1", name: "Cabelo" }]);
  api.combos.mockResolvedValue([]);
  api.service.mockResolvedValue(service);
  api.professionals.mockResolvedValue(page([person]));
  api.professional.mockResolvedValue(person);
  api.professionalServices.mockResolvedValue(["s1"]);
  api.blocks.mockResolvedValue([]);
  api.roles.mockResolvedValue([]);
  api.assignable.mockResolvedValue([]);
  vi.spyOn(window, "confirm").mockReturnValue(true);
});

describe("catálogo e equipe", () => {
  it("cadastra serviço gratuito e atualiza a listagem", async () => {
    api.createService.mockResolvedValue(service);
    show(<CatalogPage />);
    fireEvent.change(screen.getByLabelText("Nome do serviço"), { target: { value: "Livre" } });
    fireEvent.change(screen.getByLabelText("Preço (R$)"), { target: { value: "0" } });
    fireEvent.change(screen.getByLabelText("Duração (minutos)"), { target: { value: "30" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar serviço" }));
    await waitFor(() =>
      expect(api.createService).toHaveBeenCalledWith(
        expect.objectContaining({ name: "Livre", price: 0, durationMinutes: 30 }),
      ),
    );
    await waitFor(() => expect(api.services.mock.calls.length).toBeGreaterThan(2));
  });
  it("consulta e edita dados e preço, e alterna estado do serviço", async () => {
    api.updateService.mockResolvedValue(service);
    api.price.mockResolvedValue(service);
    api.deactivate.mockResolvedValue(undefined);
    show(<CatalogPage />);
    fireEvent.click(await screen.findByText("Gerenciar"));
    fireEvent.click(screen.getByRole("button", { name: "Editar serviço" }));
    const editor = (await screen.findByRole("heading", { name: "Editar serviço" })).parentElement!;
    fireEvent.change(await within(editor).findByLabelText("Nome do serviço"), {
      target: { value: "Novo" },
    });
    fireEvent.change(within(editor).getByLabelText("Categoria"), { target: { value: "c1" } });
    fireEvent.click(within(editor).getByRole("button", { name: "Salvar serviço" }));
    await waitFor(() =>
      expect(api.updateService).toHaveBeenCalledWith(
        "s1",
        expect.objectContaining({ name: "Novo", categoryId: "c1" }),
      ),
    );
    fireEvent.change(screen.getByLabelText("Novo preço (R$)"), { target: { value: "0" } });
    fireEvent.click(screen.getByRole("button", { name: "Atualizar preço" }));
    await waitFor(() => expect(api.price).toHaveBeenCalledWith("s1", 0));
    fireEvent.click(screen.getByRole("button", { name: "Desativar serviço" }));
    await waitFor(() => expect(api.deactivate).toHaveBeenCalledWith("s1"));
  });
  it("oferece ativação para serviço inativo e mostra validação do servidor", async () => {
    api.services.mockResolvedValue(page([{ ...service, isActive: false }]));
    api.activateService.mockResolvedValue(undefined);
    api.createService.mockRejectedValue(
      new ApiError("Inválido", 400, { errors: { Name: ["Nome inválido."] } }),
    );
    show(<CatalogPage />);
    fireEvent.click(await screen.findByText("Gerenciar"));
    fireEvent.click(screen.getByRole("button", { name: "Ativar serviço" }));
    await waitFor(() => expect(api.activateService).toHaveBeenCalledWith("s1"));
    const form = screen.getByRole("button", { name: "Cadastrar serviço" }).closest("form")!;
    fireEvent.change(within(form).getByLabelText("Nome do serviço"), { target: { value: "X" } });
    fireEvent.change(within(form).getByLabelText("Preço (R$)"), { target: { value: "0" } });
    fireEvent.change(within(form).getByLabelText("Duração (minutos)"), { target: { value: "30" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar serviço" }));
    expect(await screen.findByText("Nome inválido.")).toBeInTheDocument();
  });
  it("lista inativos, edita e desvincula preservando cadastro", async () => {
    api.updatePro.mockResolvedValue(person);
    api.unassignService.mockResolvedValue(undefined);
    api.deactivatePro.mockResolvedValue(undefined);
    show(<TeamPage />);
    fireEvent.click(await screen.findByLabelText("Incluir profissionais inativos"));
    await waitFor(() => expect(api.professionals).toHaveBeenCalledWith(1, 20, true));
    fireEvent.click(await screen.findByRole("button", { name: "Ver vínculos e bloqueios" }));
    const editor = (await screen.findByRole("button", { name: "Salvar profissional" })).closest(
      "form",
    )!;
    fireEvent.change(within(editor).getByLabelText("Bio"), { target: { value: "Nova bio" } });
    fireEvent.submit(editor);
    await waitFor(() =>
      expect(api.updatePro).toHaveBeenCalledWith("p1", {
        name: "Ana",
        title: "Barbeira",
        bio: "Nova bio",
      }),
    );
    fireEvent.click(await screen.findByRole("button", { name: "Desvincular serviço Corte" }));
    await waitFor(() => expect(api.unassignService).toHaveBeenCalledWith("p1", "s1"));
    fireEvent.click(screen.getByRole("button", { name: "Desativar profissional" }));
    await waitFor(() => expect(api.deactivatePro).toHaveBeenCalledWith("p1"));
  });
  it("cadastra profissional, associa serviço e exibe conflito de limite", async () => {
    api.createPro.mockResolvedValue(person);
    api.professionalServices.mockResolvedValue([]);
    api.assignService.mockRejectedValue(new ApiError("Limite do plano atingido.", 409));
    show(<TeamPage />);
    fireEvent.change(screen.getByLabelText("Nome completo"), { target: { value: "Bia" } });
    fireEvent.click(screen.getByRole("button", { name: "Cadastrar profissional" }));
    await waitFor(() =>
      expect(api.createPro).toHaveBeenCalledWith(expect.objectContaining({ name: "Bia" })),
    );
    fireEvent.click(await screen.findByRole("button", { name: "Ver vínculos e bloqueios" }));
    const select = await screen.findByLabelText("Serviço a vincular");
    fireEvent.change(select, { target: { value: "s1" } });
    fireEvent.click(screen.getByRole("button", { name: "Vincular serviço" }));
    await waitFor(() => expect(api.assignService).toHaveBeenCalledWith("p1", "s1"));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Limite do plano atingido."));
    api.assignService.mockResolvedValue(undefined);
    fireEvent.click(screen.getByRole("button", { name: "Vincular serviço" }));
    await waitFor(() => expect(api.assignService).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Serviço vinculado."));
  });
  it("ativa profissional inativo", async () => {
    api.professionals.mockResolvedValue(page([{ ...person, isActive: false }]));
    api.activatePro.mockResolvedValue(undefined);
    show(<TeamPage />);
    fireEvent.click(await screen.findByRole("button", { name: "Ativar profissional" }));
    await waitFor(() => expect(api.activatePro).toHaveBeenCalledWith("p1"));
  });
  it("restringe escrita pela permissão efetiva", async () => {
    state.permissions = ["ViewServices", "ViewTeamAgenda"];
    show(
      <>
        <CatalogPage />
        <TeamPage />
      </>,
    );
    expect(await screen.findByText("Corte")).toBeInTheDocument();
    expect(await screen.findByText("Ana")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cadastrar serviço" })).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Cadastrar profissional" }),
    ).not.toBeInTheDocument();
  });
});
