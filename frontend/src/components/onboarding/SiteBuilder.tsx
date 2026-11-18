import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { Monitor, Smartphone, Tablet } from "lucide-react";
import { toast } from "sonner";
import { Alert, Button, Spinner, TextField } from "@/components/ui-sv";
import { TenantSite } from "@/components/site/TenantSite";
import { useAuth } from "@/lib/auth/AuthProvider";
import { IS_DEMO } from "@/lib/api/http";
import { operations } from "@/lib/api/operations";
import { hasPermission } from "@/lib/permissions";
import { slugPattern } from "@/lib/api/contracts";
import {
  draftProblems,
  identificationProblems,
  generateSite,
  isSiteSegment,
  makeDraft,
  registeredSiteSegment,
  siteSegments,
  templates,
  type Draft,
} from "@/lib/site-templates";
import { loadWorkspace, persistWorkspace, type SiteWorkspace } from "@/lib/site-workspace";
import { OnboardingTopbar } from "./OnboardingTopbar";
import { SiteAssets } from "./SiteAssets";
import s from "./styles";

const steps = ["Identificação", "Identidade visual", "Catálogo", "Revisão"];
const devices = [
  { name: "Desktop", width: 1280, Icon: Monitor },
  { name: "Tablet", width: 768, Icon: Tablet },
  { name: "Celular", width: 375, Icon: Smartphone },
];
const states = {
  Reviewing: "Rascunho",
  AwaitingApproval: "Aguardando aprovação",
  Approved: "Aprovado",
  Published: "Publicado na demonstração",
};

export function SiteBuilder() {
  const { user } = useAuth();
  const scope = user
    ? IS_DEMO
      ? `demo:${user.id}`
      : user.tenantId
        ? `tenant:${user.tenantId}`
        : undefined
    : undefined;
  const canEdit = !!scope && hasPermission(user, "ManageTenantSettings");
  const saved = useQuery({
    queryKey: ["site-workspace", scope],
    queryFn: async () => (await loadWorkspace(scope!)) ?? null,
    enabled: canEdit,
  });
  if (!user || !scope || !canEdit)
    return (
      <Alert tone="warning">
        A criação de sites exige um perfil de administração do estabelecimento.
      </Alert>
    );
  if (saved.isPending) return <Spinner label="Carregando rascunho…" />;
  if (saved.isError)
    return (
      <Alert tone="error">
        O rascunho deste navegador está indisponível.{" "}
        <Button onClick={() => void saved.refetch()}>Tentar novamente</Button>
      </Alert>
    );
  const defaultSegment = registeredSiteSegment(user.segment);
  return (
    <Builder
      key={scope}
      initial={
        saved.data ?? {
          scope,
          status: "Reviewing",
          step: 0,
          draft: makeDraft(defaultSegment ?? "Barbershop", user.establishmentName),
        }
      }
    />
  );
}

function Builder({ initial }: { initial: SiteWorkspace }) {
  const { user } = useAuth();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [workspace, setWorkspace] = useState(initial);
  const currentWorkspace = useRef(initial);
  const [dirty, setDirty] = useState(false);
  const [device, setDevice] = useState(() =>
    window.matchMedia("(max-width: 760px)").matches ? 2 : 0,
  );
  const viewport = useRef<HTMLDivElement>(null);
  const [previewWidth, setPreviewWidth] = useState(0);
  const deviceWidth = devices[device]!.width;
  const [slug, setSlug] = useState(initial.publication?.slug ?? "");
  const [validation, setValidation] = useState("");
  const draft = workspace.draft;
  const step = workspace.step;
  const locked = workspace.status !== "Reviewing";
  const catalog = useQuery({
    queryKey: ["panel", "services", "site-catalog", initial.scope],
    queryFn: () => operations.services(1, true, 100),
    enabled: hasPermission(user, "ViewServices"),
  });
  const categories = useQuery({
    queryKey: ["panel", "services", "categories", initial.scope],
    queryFn: operations.categories,
    enabled: step === 2 && hasPermission(user, "ManageServices"),
  });
  const people = useQuery({
    queryKey: ["panel", "team", "site-catalog", initial.scope],
    queryFn: () => operations.professionals(1, 100),
    enabled: hasPermission(user, "ViewTeamAgenda"),
  });
  const professionals = (people.data?.items ?? [])
    .filter((person) => person.isActive && person.isServiceProvider)
    .map(({ id, name }) => ({ id, name, serviceIds: [] }));
  const services = (catalog.data?.items ?? []).map((entry) => ({
    ...entry,
    currency: "BRL",
    description: entry.description ?? null,
  }));
  const site = workspace.approved ?? generateSite(draft, professionals.length > 0);
  const save = useMutation({
    mutationFn: ({ next }: { previous: SiteWorkspace; next: SiteWorkspace }) =>
      persistWorkspace(next),
    onSuccess: (_result, { previous, next }) => {
      const current = currentWorkspace.current;
      const updated =
        current === previous || next.status !== "Reviewing"
          ? next
          : { ...next, draft: current.draft };
      currentWorkspace.current = updated;
      setWorkspace(updated);
      setDirty(updated !== next);
      qc.setQueryData(["site-workspace", initial.scope], next);
      if (next.publication !== previous.publication && next.publication) {
        void qc.invalidateQueries({ queryKey: ["public-site", next.publication.slug] });
        void qc.invalidateQueries({ queryKey: ["panel", "dashboard"] });
      }
    },
  });
  const createService = useMutation({
    mutationFn: operations.createService,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["panel", "services"] }),
  });
  const busy = save.isPending || createService.isPending;

  useEffect(() => {
    const element = viewport.current;
    if (!element) return;
    const measure = () => setPreviewWidth(element.clientWidth);
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    measure();
    return () => observer.disconnect();
  }, []);

  const { mutate: saveChanges } = save;
  useEffect(() => {
    if (!dirty || busy || save.isError) return;
    const timer = window.setTimeout(
      () => saveChanges({ previous: workspace, next: workspace }),
      750,
    );
    return () => window.clearTimeout(timer);
  }, [dirty, busy, save.isError, saveChanges, workspace]);

  async function saveBeforeExit() {
    if (busy) return false;
    if (!dirty) return true;
    const next = currentWorkspace.current;
    try {
      await save.mutateAsync({ previous: next, next });
      return true;
    } catch {
      return false;
    }
  }

  function store(next: SiteWorkspace, message?: string) {
    setValidation("");
    saveChanges(
      { previous: currentWorkspace.current, next },
      {
        onSuccess: () => {
          if (message) toast.success(message);
        },
      },
    );
  }

  function patch(update: Partial<Draft>) {
    if (currentWorkspace.current.status !== "Reviewing") return;
    if (save.isError) save.reset();
    setValidation("");
    setDirty(true);
    const current = currentWorkspace.current;
    const next = { ...current, draft: { ...current.draft, ...update } };
    currentWorkspace.current = next;
    setWorkspace(next);
  }

  function next() {
    if (step === 0) {
      const issues = identificationProblems(draft);
      if (issues.length) return setValidation(issues[0]!);
    }
    if (
      step === 2 &&
      (!catalog.isSuccess ||
        !catalog.data.totalCount ||
        draft.intendedProfessionals < 1 ||
        !Number.isInteger(draft.intendedProfessionals))
    )
      return setValidation("Confirme um serviço ativo e a quantidade pretendida de profissionais.");
    store({ ...workspace, step: Math.min(step + 1, 3) });
  }

  function requestApproval() {
    const errors = draftProblems(draft, catalog.data?.totalCount ?? 0);
    if (errors.length) return setValidation(errors.join(" "));
    store(
      { ...workspace, status: "AwaitingApproval", approved: site },
      IS_DEMO ? "Versão enviada para aprovação." : "Versão salva para revisão neste navegador.",
    );
  }
  function publish(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!IS_DEMO || !workspace.approved || workspace.status !== "Approved") return;
    const address = slug.trim();
    if (!new RegExp("^" + slugPattern + "$").test(address))
      return setValidation("Use de 3 a 40 letras minúsculas, números ou hífens entre caracteres.");
    store(
      {
        ...workspace,
        status: "Published",
        publication: {
          slug: address,
          site: workspace.approved,
          date: new Date().toISOString(),
        },
      },
      "Site publicado na demonstração.",
    );
  }
  function addService(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const name = String(data.get("servico") ?? "").trim();
    const price = Number(data.get("preco"));
    const description = String(data.get("descricao") ?? "").trim();
    const categoryId = String(data.get("categoria") ?? "");
    setValidation("");
    const durationMinutes = Number(data.get("duracao"));
    if (
      !name ||
      !Number.isFinite(price) ||
      price <= 0 ||
      !Number.isInteger(durationMinutes) ||
      durationMinutes < 1
    ) {
      setValidation("Confira o nome, o valor e a duração do atendimento.");
      return;
    }
    createService.mutate(
      {
        name,
        price,
        durationMinutes,
        highlight: false,
        ...(description ? { description } : {}),
        ...(categories.data?.some((category) => category.id === categoryId) ? { categoryId } : {}),
      },
      {
        onSuccess: () => {
          toast.success("Serviço adicionado ao catálogo.");
          form.reset();
        },
      },
    );
  }

  let editor: ReactNode;
  if (step === 0)
    editor = (
      <>
        <TextField
          label="Nome público"
          name="negocio"
          value={draft.name}
          maxLength={150}
          onChange={(e) => patch({ name: e.target.value })}
          required
        />
        <label className={s.builderField}>
          Segmento
          <select
            value={draft.segment}
            onChange={(e) => {
              const segment = e.target.value;
              if (isSiteSegment(segment)) patch({ segment, palette: 0 });
            }}
          >
            {Object.entries(siteSegments).map(([value, label]) => (
              <option value={value} key={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className={s.builderField}>
          Descrição do estabelecimento
          <textarea
            rows={4}
            maxLength={1500}
            value={draft.description}
            onChange={(e) => patch({ description: e.target.value })}
            required
          />
        </label>
        <TextField
          label="Telefone público"
          type="tel"
          value={draft.phone}
          maxLength={30}
          onChange={(e) => patch({ phone: e.target.value })}
          required
        />
        <label className={s.builderCheck}>
          <input
            type="checkbox"
            checked={draft.showLocation}
            onChange={(e) => patch({ showLocation: e.target.checked })}
          />{" "}
          Exibir localização no site
        </label>
        {draft.showLocation && (
          <TextField
            label="Localização pública"
            hint="Pode ser a cidade ou a região."
            value={draft.address}
            maxLength={200}
            onChange={(e) => patch({ address: e.target.value })}
          />
        )}
      </>
    );
  else if (step === 1)
    editor = (
      <>
        <fieldset className={s.builderPalettes}>
          <legend>Paleta do site</legend>
          {templates[draft.segment].palettes.map((option, index) => (
            <label className={s.builderPalette} key={option.label}>
              <input
                type="radio"
                name="paleta"
                checked={draft.palette === index}
                onChange={() => patch({ palette: index })}
              />
              <span aria-hidden="true">
                {option.colors.slice(0, 3).map((color) => (
                  <i key={color} style={{ backgroundColor: color }} />
                ))}
              </span>
              {option.label}
            </label>
          ))}
        </fieldset>
        <SiteAssets
          label="Logo"
          images={draft.logo ? [draft.logo] : []}
          onChange={([logo]) => patch({ logo })}
        />
        {draft.logo && (
          <label className={s.builderCheck}>
            <input
              type="checkbox"
              checked={draft.useLogoIcon ?? false}
              onChange={(e) => patch({ useLogoIcon: e.target.checked })}
            />{" "}
            Usar o logo como ícone do site
          </label>
        )}
        <SiteAssets
          label="Banner"
          images={draft.banner ? [draft.banner] : []}
          onChange={([banner]) => patch({ banner })}
        />
        <SiteAssets
          label="Galeria"
          images={draft.gallery}
          onChange={(gallery) => patch({ gallery })}
          max={8}
          describe
        />
      </>
    );
  else if (step === 2)
    editor = (
      <>
        {catalog.isPending ? (
          <Spinner label="Carregando catálogo…" />
        ) : catalog.isError ? (
          <Alert tone="error">
            O catálogo está indisponível.{" "}
            <Button variant="ghost" onClick={() => void catalog.refetch()}>
              Tentar novamente
            </Button>
          </Alert>
        ) : (
          <p>{catalog.data.totalCount} serviço(s) ativo(s) no catálogo.</p>
        )}
        {hasPermission(user, "ManageServices") && (
          <form onSubmit={addService} className={s.builderService}>
            <TextField label="Nome do serviço" name="servico" maxLength={150} required />
            <TextField
              label="Duração em minutos"
              name="duracao"
              type="number"
              min="1"
              max="1440"
              required
            />
            <TextField
              label="Preço em reais"
              name="preco"
              type="number"
              min="0.01"
              step="0.01"
              required
            />
            <label className={s.builderField}>
              Descrição do serviço (opcional)
              <textarea name="descricao" rows={2} maxLength={1000} />
            </label>
            {!!categories.data?.length && (
              <label className={s.builderField}>
                Categoria (opcional)
                <select name="categoria">
                  <option value="">Sem categoria</option>
                  {categories.data.map(({ id, name }) => (
                    <option value={id} key={id}>
                      {name}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {createService.isError && <Alert tone="error">{createService.error.message}</Alert>}
            <Button type="submit" variant="ghost" loading={createService.isPending}>
              Adicionar ao catálogo
            </Button>
          </form>
        )}
        <TextField
          label="Quantidade pretendida de profissionais"
          type="number"
          min="1"
          value={draft.intendedProfessionals || ""}
          onChange={(e) => patch({ intendedProfessionals: Number(e.target.value) })}
        />
      </>
    );
  else
    editor = (
      <>
        <TextField
          label="Título do site"
          placeholder={draft.name}
          value={draft.title}
          maxLength={150}
          onChange={(e) => patch({ title: e.target.value })}
        />
        <TextField
          label="Subtítulo"
          placeholder={draft.description}
          value={draft.subtitle}
          maxLength={260}
          onChange={(e) => patch({ subtitle: e.target.value })}
        />
        <TextField
          label="Texto do botão de agendamento"
          value={draft.cta}
          maxLength={48}
          onChange={(e) => patch({ cta: e.target.value })}
          required
        />
        <fieldset className={s.builderAssets}>
          <legend>Seções opcionais</legend>
          {(
            [
              ["showAbout", "Sobre o espaço"],
              ["showTeam", "Equipe"],
              ["showGallery", "Galeria"],
            ] as const
          ).map(([field, label]) => (
            <label className={s.builderCheck} key={field}>
              <input
                type="checkbox"
                checked={draft[field]}
                onChange={(e) => patch({ [field]: e.target.checked })}
              />{" "}
              {label}
            </label>
          ))}
        </fieldset>
      </>
    );

  return (
    <>
      <OnboardingTopbar
        progress={{ current: step + 1, total: steps.length, label: steps[step]! }}
        onBeforeExit={saveBeforeExit}
      />
      <main className={s.builder}>
        <header className={s.builderHeader}>
          <div>
            <h1>Configure seu site</h1>
            <p>
              {siteSegments[draft.segment]} · {states[workspace.status]}
            </p>
          </div>
          <Link
            to="/painel/site"
            onClick={async (event) => {
              if (
                (!dirty && !busy) ||
                event.ctrlKey ||
                event.metaKey ||
                event.shiftKey ||
                event.altKey
              )
                return;
              event.preventDefault();
              if (await saveBeforeExit()) await navigate({ to: "/painel/site" });
            }}
          >
            Meu site
          </Link>
        </header>
        {!IS_DEMO && (
          <Alert tone="info">
            O rascunho fica neste navegador. A aprovação e publicação dos novos templates dependem
            do contrato de gravação da configuração, tema e assets no servidor.
          </Alert>
        )}
        {workspace.publication && (
          <Alert tone="success">
            <Link to="/" search={{ siteDemo: workspace.publication.slug }}>
              Abrir site de demonstração publicado
            </Link>
          </Alert>
        )}
        <div className={s.builderLayout}>
          <aside className={s.builderEditor}>
            <details open>
              <summary>{steps[step]}</summary>
              <fieldset className={s.builderControls} disabled={locked || busy}>
                {editor}
              </fieldset>
              {validation && <Alert tone="error">{validation}</Alert>}
              {save.isError && <Alert tone="error">{save.error.message}</Alert>}
              <div className={s.builderActions}>
                {!locked && (
                  <>
                    {step > 0 && (
                      <Button
                        variant="subtle"
                        disabled={busy}
                        onClick={() => store({ ...workspace, step: step - 1 })}
                      >
                        Voltar
                      </Button>
                    )}
                    <Button
                      variant="ghost"
                      disabled={busy}
                      onClick={() => store(workspace, "Rascunho salvo neste navegador.")}
                    >
                      Salvar rascunho
                    </Button>
                    {step < 3 ? (
                      <Button disabled={busy} onClick={next}>
                        Continuar
                      </Button>
                    ) : (
                      <Button disabled={busy} onClick={requestApproval}>
                        {IS_DEMO ? "Enviar para aprovação" : "Salvar versão para revisão"}
                      </Button>
                    )}
                  </>
                )}
                {locked && (
                  <Button
                    variant="ghost"
                    disabled={busy}
                    onClick={() => {
                      const { approved, ...editable } = workspace;
                      store({ ...editable, status: "Reviewing", step: 3 });
                    }}
                  >
                    Voltar à edição
                  </Button>
                )}
                {workspace.status === "AwaitingApproval" && (
                  <Button
                    disabled={busy}
                    onClick={() => store({ ...workspace, status: "Approved" }, "Versão aprovada.")}
                  >
                    {IS_DEMO ? "Aprovar esta versão" : "Aprovar neste navegador"}
                  </Button>
                )}
              </div>
              {workspace.status === "Approved" && IS_DEMO && (
                <form onSubmit={publish} className={s.builderPublish}>
                  <TextField
                    label="Endereço da demonstração"
                    value={slug}
                    onChange={(e) => setSlug(e.target.value)}
                    pattern={slugPattern}
                    hint="Use de 3 a 40 letras minúsculas, números ou hífens entre caracteres."
                    maxLength={40}
                    required
                  />
                  <Button type="submit" loading={save.isPending}>
                    Publicar na demonstração
                  </Button>
                </form>
              )}
            </details>
            <p className={s.builderSaveState} role="status">
              {save.isPending ? "Salvando…" : dirty ? "Alterações pendentes" : "Rascunho local"}
            </p>
          </aside>
          <section className={s.builderPreview} aria-labelledby="preview-heading">
            <div className={s.builderBar}>
              <h2 id="preview-heading">Prévia do site</h2>
              <div role="group" aria-label="Largura da prévia">
                {devices.map(({ name, Icon }, index) => (
                  <button
                    key={name}
                    type="button"
                    aria-pressed={device === index}
                    onClick={() => setDevice(index)}
                  >
                    <Icon size={16} aria-hidden="true" /> {name}
                  </button>
                ))}
              </div>
            </div>
            <div
              ref={viewport}
              className={s.builderViewport}
              tabIndex={0}
              aria-label={`Prévia em ${devices[device]!.name}`}
            >
              <div
                style={{
                  width: deviceWidth,
                  zoom: previewWidth ? Math.min(1, (previewWidth - 1) / deviceWidth) : undefined,
                }}
                className={s.builderFrame}
              >
                <TenantSite site={site} services={services} professionals={professionals} />
              </div>
            </div>
          </section>
        </div>
      </main>
    </>
  );
}
