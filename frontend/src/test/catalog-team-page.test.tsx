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
  block: vi.fn(),
  blockDetail: vi.fn(),
  deleteBlock: vi.fn(),
  professionalSchedule: vi.fn(),
  updateSchedule: vi.fn(),
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
  api.professionalSchedule.mockResolvedValue({
    workDays: ["Monday", "Wednesday"],
    startTime: "09:00:00",
    endTime: "18:00:00",
    slotIntervalMinutes: 30,
  });
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

describe("jornada e bloqueios", () => {
  async function openPerson() {
    show(<TeamPage />);
    fireEvent.click((await screen.findAllByRole("button", { name: "Ver vínculos e bloqueios" }))[0]!);
    await screen.findByText(/Jornada salva:/);
  }

  it("exibe jornada e salva uma semana fechada", async () => {
    api.updateSchedule.mockImplementation(async (_id, input) => {
      api.professionalSchedule.mockResolvedValue(input);
      return input;
    });
    await openPerson();
    expect(screen.getByText(/Jornada salva: Segunda-feira, Quarta-feira/)).toBeInTheDocument();
    const form = screen.getByRole("button", { name: "Salvar jornada" }).closest("form")!;
    fireEvent.click(within(form).getByLabelText("Segunda-feira"));
    fireEvent.click(within(form).getByLabelText("Quarta-feira"));
    fireEvent.change(within(form).getByLabelText("Início da jornada individual"), {
      target: { value: "10:00" },
    });
    fireEvent.click(within(form).getByRole("button", { name: "Salvar jornada" }));
    await waitFor(() =>
      expect(api.updateSchedule).toHaveBeenCalledWith("p1", {
        workDays: [],
        startTime: "10:00:00",
        endTime: "18:00:00",
        slotIntervalMinutes: 30,
      }),
    );
    expect(await screen.findByText(/Jornada salva: Semana fechada/)).toBeInTheDocument();
  });

  it("salva jornada aberta e mantém dados separados ao trocar de profissional", async () => {
    const other = { ...person, id: "p2", name: "Bia" };
    api.professionals.mockResolvedValue(page([person, other]));
    api.professionalSchedule.mockImplementation(async (id: string) => id === "p2"
      ? { workDays: ["Friday"], startTime: "11:00:00", endTime: "17:00:00", slotIntervalMinutes: 20 }
      : { workDays: ["Monday", "Wednesday"], startTime: "09:00:00", endTime: "18:00:00", slotIntervalMinutes: 30 });
    api.updateSchedule.mockResolvedValue({ workDays: ["Monday", "Wednesday", "Friday"], startTime: "09:00:00", endTime: "18:00:00", slotIntervalMinutes: 30 });
    await openPerson();
    const form = screen.getByRole("button", { name: "Salvar jornada" }).closest("form")!;
    fireEvent.click(within(form).getByLabelText("Sexta-feira"));
    fireEvent.click(within(form).getByRole("button", { name: "Salvar jornada" }));
    await waitFor(() => expect(api.updateSchedule).toHaveBeenCalledWith("p1", expect.objectContaining({ workDays: ["Monday", "Wednesday", "Friday"] })));
    const otherRow = screen.getByText("Bia", { exact: true }).closest("li")!;
    fireEvent.click(within(otherRow).getByRole("button", { name: "Ver vínculos e bloqueios" }));
    expect(await screen.findByText(/Jornada salva: Sexta-feira · 11:00–17:00/)).toBeInTheDocument();
  });

  it("mantém jornada salva e permite recuperá-la após conflito", async () => {
    api.updateSchedule.mockRejectedValue(new ApiError("Limite do plano atingido", 409));
    await openPerson();
    let form = screen.getByRole("button", { name: "Salvar jornada" }).closest("form")!;
    fireEvent.click(within(form).getByLabelText("Terça-feira"));
    fireEvent.click(within(form).getByRole("button", { name: "Salvar jornada" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Limite do plano atingido"));
    expect(toast.success).not.toHaveBeenCalledWith("Jornada atualizada.");
    expect(screen.getByText(/Jornada salva: Segunda-feira, Quarta-feira/)).toBeInTheDocument();
    fireEvent.click(within(form).getByRole("button", { name: "Recuperar jornada salva" }));
    await waitFor(() => {
      form = screen.getByRole("button", { name: "Salvar jornada" }).closest("form")!;
      expect(within(form).getByLabelText("Terça-feira")).not.toBeChecked();
    });
  });

  it("consulta detalhes e remove bloqueio atualizando a lista", async () => {
    const block = {
      id: "b1",
      start: "2026-11-23T12:00:00Z",
      end: "2026-11-23T13:00:00Z",
      reason: "Pausa",
    };
    api.blocks.mockResolvedValue([block]);
    api.blockDetail.mockResolvedValue(block);
    api.deleteBlock.mockImplementation(async () => {
      api.blocks.mockResolvedValue([]);
    });
    await openPerson();
    fireEvent.click(await screen.findByRole("button", { name: "Ver detalhes do bloqueio" }));
    await waitFor(() => expect(api.blockDetail).toHaveBeenCalledWith("p1", "b1"));
    expect(
      await screen.findByRole("heading", { name: "Detalhes do bloqueio" }),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Remover bloqueio" }));
    await waitFor(() => expect(api.deleteBlock).toHaveBeenCalledWith("p1", "b1"));
    expect(await screen.findByText("Nenhum horário bloqueado.")).toBeInTheDocument();
  });

  it("envia instantes UTC a partir do horário local e exibe erro da API", async () => {
    api.block.mockRejectedValue(new ApiError("O término deve ser posterior ao início.", 400));
    await openPerson();
    fireEvent.change(screen.getByLabelText("Início do bloqueio"), {
      target: { value: "2026-11-23T09:00" },
    });
    fireEvent.change(screen.getByLabelText("Fim do bloqueio"), {
      target: { value: "2026-11-23T10:00" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Bloquear horário" }));
    await waitFor(() =>
      expect(api.block).toHaveBeenCalledWith("p1", {
        start: new Date("2026-11-23T09:00").toISOString(),
        end: new Date("2026-11-23T10:00").toISOString(),
      }),
    );
    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith("O término deve ser posterior ao início."),
    );
  });

  it("cria bloqueio e atualiza a listagem", async () => {
    const block = { id: "b2", start: "2026-11-23T12:00:00Z", end: "2026-11-23T13:00:00Z", reason: "Reunião" };
    api.block.mockImplementation(async () => { api.blocks.mockResolvedValue([block]); return block; });
    await openPerson();
    fireEvent.change(screen.getByLabelText("Início do bloqueio"), { target: { value: "2026-11-23T09:00" } });
    fireEvent.change(screen.getByLabelText("Fim do bloqueio"), { target: { value: "2026-11-23T10:00" } });
    fireEvent.click(screen.getByRole("button", { name: "Bloquear horário" }));
    expect(await screen.findByText(/Reunião/)).toBeInTheDocument();
    expect(api.blocks.mock.calls.length).toBeGreaterThan(1);
  });

  it("permite somente leitura sem ManageTeam", async () => {
    state.permissions = ["ViewServices", "ViewTeamAgenda"];
    await openPerson();
    expect(screen.getByText(/Jornada salva:/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Salvar jornada" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Bloquear horário" })).not.toBeInTheDocument();
  });
});
