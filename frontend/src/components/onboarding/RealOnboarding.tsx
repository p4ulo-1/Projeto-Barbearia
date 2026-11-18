import { useState, type FormEvent, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { SegmentField } from "@/components/auth/SegmentField";
import { Alert, Button, SelectField, Spinner, TextField } from "@/components/ui-sv";
import { PageHeader } from "@/components/panel/PanelPage";
import { OnboardingTopbar } from "./OnboardingTopbar";
import { realOnboarding } from "@/lib/api/onboarding-real";
import { slugPattern, type Session } from "@/lib/api/contracts";
import { segmentCode } from "@/lib/api/auth";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { incompleteSections } from "@/lib/site-schema";
import { SiteSections } from "@/components/site/SiteSections";
import s from "@/components/panel/styles";

const api = realOnboarding;
const key = ["onboarding", "real"];
const states: Record<Session["status"], string> = {
  Collecting: "Coleta de informações",
  Reviewing: "Revisão do rascunho",
  AwaitingApproval: "Aguardando aprovação",
  Approved: "Aprovado para publicação",
  Published: "Publicado",
};

export function RealOnboarding({ preview: showPreview = false }: { preview?: boolean }) {
  const { user, refresh } = useAuth();
  const read = hasPermission(user, "ViewTenantSettings");
  const write = hasPermission(user, "ManageTenantSettings");
  const qc = useQueryClient();
  const latest = useQuery({
    queryKey: key,
    queryFn: async () => (await api.latest()) ?? null,
    enabled: read,
  });
  const session = latest.data;
  const id = session?.id ?? "";
  const status = session?.status;
  const collecting = status === "Collecting";
  const [turnReady, setTurnReady] = useState(false);
  const [feedback, setFeedback] = useState("");
  const messages = useQuery({
    queryKey: [...key, id, "messages"],
    queryFn: () => api.messages(id),
    enabled: !!id && collecting,
  });
  const assets = useQuery({
    queryKey: [...key, id, "assets"],
    queryFn: () => api.assets(id),
    enabled: !!id && collecting,
  });
  const preview = useQuery({
    queryKey: [...key, id, "preview"],
    queryFn: () => api.preview(id),
    enabled: !!session && !collecting && status !== "Published",
  });
  const missing = preview.data ? incompleteSections(preview.data.sections) : [];
  const operation = useMutation({
    mutationFn: (action: () => Promise<unknown>) => action(),
    async onSuccess() {
      await qc.invalidateQueries({ queryKey: key });
      await qc.invalidateQueries({ queryKey: ["panel"] });
    },
  });
  const handle = (
    event: FormEvent<HTMLFormElement>,
    action: (form: FormData) => Promise<unknown>,
    success: string,
    reset = false,
  ) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    operation.mutate(() => action(data), {
      onSuccess() {
        if (reset) form.reset();
        setFeedback(success);
      },
    });
  };
  const act = (action: () => Promise<unknown>, success: string) => {
    setFeedback("");
    operation.mutate(action, { onSuccess: () => setFeedback(success) });
  };
  const value = (form: FormData, field: string) => String(form.get(field) ?? "").trim();

  let content: ReactNode;
  if (!read)
    content = <Alert tone="warning">Seu perfil não tem acesso à configuração do site.</Alert>;
  else if (latest.isPending) content = <Spinner />;
  else if (latest.isError)
    content = (
      <>
        <Alert tone="error">{latest.error.message}</Alert>
        <Button onClick={() => void latest.refetch()}>Tentar novamente</Button>
      </>
    );
  else if (!session)
    content = write ? (
      <form
        className={s.formGrid}
        onSubmit={(event) =>
          handle(
            event,
            (form) => api.start(value(form, "businessName"), segmentCode(value(form, "segment"))),
            "Configuração iniciada.",
          )
        }
      >
        <TextField
          label="Nome do estabelecimento"
          name="businessName"
          defaultValue={
            user?.establishmentName === "Seu estabelecimento" ? "" : user?.establishmentName
          }
          required
          maxLength={150}
        />
        <SegmentField {...(user?.segment ? { defaultValue: user.segment } : {})} />
        <Button type="submit" loading={operation.isPending}>
          Iniciar configuração
        </Button>
      </form>
    ) : (
      <Alert>Nenhuma configuração foi iniciada.</Alert>
    );
  else if (status === "Published")
    content = (
      <Alert tone="success">
        Seu site foi publicado. Acompanhe a publicação em <Link to="/painel/site">Meu site</Link>.
      </Alert>
    );
  else if (collecting)
    content = (
      <div className={s.moduleGrid}>
        <section className={s.moduleCard}>
          <h2>Conversa com a assistente</h2>
          {messages.isPending ? (
            <Spinner />
          ) : messages.isError ? (
            <Alert tone="error">{messages.error.message}</Alert>
          ) : (
            <div className={s.messageList} role="log" aria-label="Histórico da conversa">
              {messages.data.length === 0 && (
                <p>Conte sobre os serviços e as informações que deseja mostrar no site.</p>
              )}
              {messages.data.map((message, index) => (
                <article key={message.id ?? index} className={s.message}>
                  <strong>
                    {message.role.toLowerCase() === "assistant" ? "Assistente" : "Você"}
                  </strong>
                  <p>{message.content ?? message.text ?? message.message}</p>
                </article>
              ))}
            </div>
          )}
          {write && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                handle(
                  event,
                  async (form) => {
                    const turn = await api.send(id, value(form, "message"));
                    setTurnReady(turn.isComplete);
                  },
                  "Mensagem enviada.",
                  true,
                )
              }
            >
              <TextField label="Sua mensagem" name="message" maxLength={4000} required />
              <Button type="submit" loading={operation.isPending}>
                Enviar mensagem
              </Button>
            </form>
          )}
          {write && (
            <Button
              variant="subtle"
              disabled={operation.isPending || (!turnReady && !session.messageCount)}
              onClick={() => act(() => api.generate(id), "Rascunho gerado.")}
            >
              Gerar prévia do site
            </Button>
          )}
        </section>
        <section className={s.moduleCard}>
          <h2>Imagens do site</h2>
          <p>
            PNG ou JPEG, até 5 MB e 8.000 pixels por dimensão. Aprove as imagens que deseja usar.
          </p>
          {assets.isPending ? (
            <Spinner />
          ) : assets.isError ? (
            <Alert tone="error">{assets.error.message}</Alert>
          ) : (
            <ul className={s.recordList}>
              {assets.data.map((asset) => (
                <li key={asset.id}>
                  <span>
                    {asset.fileName} ·{" "}
                    {
                      { Pending: "Pendente", Approved: "Aprovada", Rejected: "Rejeitada" }[
                        asset.status
                      ]
                    }
                  </span>
                  {write && asset.status === "Pending" && (
                    <div className={s.pageActions}>
                      <Button
                        variant="subtle"
                        disabled={operation.isPending}
                        onClick={() =>
                          act(() => api.assetDecision(id, asset.id, true), "Imagem aprovada.")
                        }
                      >
                        Aprovar
                      </Button>
                      <form
                        className={s.formGrid}
                        onSubmit={(event) =>
                          handle(
                            event,
                            (form) => api.assetDecision(id, asset.id, false, value(form, "reason")),
                            "Imagem rejeitada.",
                          )
                        }
                      >
                        <TextField
                          name="reason"
                          label="Motivo da rejeição"
                          maxLength={300}
                          required
                        />
                        <Button type="submit" variant="ghost" disabled={operation.isPending}>
                          Rejeitar
                        </Button>
                      </form>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
          {write && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                handle(
                  event,
                  (form) => api.upload(id, value(form, "kind"), form.get("file") as File),
                  "Imagem enviada para revisão.",
                  true,
                )
              }
            >
              <SelectField
                label="Tipo de imagem"
                name="kind"
                options={[
                  "Logo",
                  "EstablishmentPhoto",
                  "ProfessionalPhoto",
                  "ServiceImage",
                  "SupportingImage",
                ]}
              />
              <TextField
                label="Arquivo"
                type="file"
                name="file"
                accept=".png,.jpg,.jpeg,image/png,image/jpeg"
                required
              />
              <Button type="submit" loading={operation.isPending}>
                Enviar imagem
              </Button>
            </form>
          )}
        </section>
      </div>
    );
  else
    content = (
      <>
        {!showPreview && (
          <Link className={s.shortcut} to="/onboarding/previa">
            Abrir prévia
          </Link>
        )}
        {preview.isPending ? (
          <Spinner />
        ) : preview.isError ? (
          <Alert tone="error">{preview.error.message}</Alert>
        ) : (
          <section className={s.moduleCard}>
            <h2>Prévia privada</h2>
            <p>{preview.data.summary}</p>
            {preview.data.note && <Alert>{preview.data.note}</Alert>}
            {missing.length > 0 && (
              <Alert tone="warning">
                Complete as seções {missing.map((section) => section.componentType).join(", ")}{" "}
                antes de publicar.
              </Alert>
            )}
            <SiteSections sections={preview.data.sections} theme={preview.data.theme} />
          </section>
        )}
        {write && preview.isSuccess && (
          <section className={s.moduleCard}>
            {status === "Reviewing" && (
              <Button
                disabled={operation.isPending || missing.length > 0}
                onClick={() => act(() => api.submit(id), "Versão submetida para aprovação.")}
              >
                Submeter para aprovação
              </Button>
            )}
            {status === "AwaitingApproval" && (
              <Button
                disabled={operation.isPending || missing.length > 0}
                onClick={() => {
                  if (window.confirm("Aprovar explicitamente a versão exibida na prévia?"))
                    act(() => api.approve(id), "Versão aprovada.");
                }}
              >
                Aprovar versão
              </Button>
            )}
            {status === "Approved" && (
              <form
                className={s.formGrid}
                onSubmit={(event) =>
                  handle(
                    event,
                    async (form) => {
                      await api.publish(id, value(form, "subdomain"));
                      await refresh();
                    },
                    "Site publicado.",
                  )
                }
              >
                <TextField
                  label="Subdomínio público"
                  name="subdomain"
                  pattern={slugPattern}
                  required
                  maxLength={40}
                />
                <Button type="submit" loading={operation.isPending} disabled={missing.length > 0}>
                  Publicar site
                </Button>
              </form>
            )}
            {!showPreview && (
              <form
                className={s.formGrid}
                onSubmit={(event) =>
                  handle(
                    event,
                    (form) => api.adjust(id, value(form, "instruction")),
                    "Ajustes aplicados. Revise a nova prévia.",
                    true,
                  )
                }
              >
                <TextField
                  label="Ajustes solicitados"
                  name="instruction"
                  required
                  maxLength={2000}
                />
                <Button type="submit" variant="subtle" loading={operation.isPending}>
                  Solicitar ajustes
                </Button>
              </form>
            )}
          </section>
        )}
      </>
    );

  return (
    <>
      <OnboardingTopbar />
      <main className={s.standalone}>
        <PageHeader
          title="Configuração do site"
          description={status ? states[status] : "Crie o site do seu estabelecimento."}
          actions={
            <Link className={s.shortcut} to="/painel">
              Ir para o painel
            </Link>
          }
        />
        {operation.isError && <Alert tone="error">{operation.error.message}</Alert>}
        {feedback && <Alert tone="success">{feedback}</Alert>}
        {content}
      </main>
    </>
  );
}
