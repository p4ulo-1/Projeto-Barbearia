import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@/components/ui-sv";
import { operations as api } from "@/lib/api/operations";
import { tenantPattern } from "@/lib/api/contracts";
import { hasPermission } from "@/lib/permissions";
import { useAuth } from "@/lib/auth/AuthProvider";
import { ModuleCard, Pagination, QueryState, RecordSelect } from "./OperationsUI";
import { field, localDate, numberField, optional, useOperation } from "./forms";
import { fmtDateTime, fmtMoney } from "./status";
import s from "./styles";

export function OptionalModules() {
  const { user } = useAuth();
  const viewCatalog = hasPermission(user, "ViewServices");
  const manageCatalog = hasPermission(user, "ManageServices");
  const viewCustomers = hasPermission(user, "ViewCustomers");
  const manageCustomers = hasPermission(user, "ManageCustomers");
  const viewReports = hasPermission(user, "ViewFinancialReports");
  const packages = useQuery({
    queryKey: ["panel", "packages"],
    queryFn: api.packages,
    enabled: viewCatalog,
  });
  const plans = useQuery({
    queryKey: ["panel", "memberships"],
    queryFn: api.membershipPlans,
    enabled: viewCatalog,
  });
  const [customerId, setCustomer] = useState("");
  const [from, setFrom] = useState(() => localDate().slice(0, 7) + "-01");
  const [to, setTo] = useState(localDate);
  const loyalty = useQuery({
    queryKey: ["panel", "loyalty", customerId],
    queryFn: () => api.loyalty(customerId),
    enabled: viewCustomers && !!customerId,
  });
  const nps = useQuery({
    queryKey: ["panel", "nps", from, to],
    queryFn: () => api.nps(from, to),
    enabled: viewReports && !!from && !!to && from <= to,
  });
  const op = useOperation();

  return (
    <ModuleCard title="Módulos de atendimento">
      {op.feedback}
      {viewCatalog && (
        <details>
          <summary>Pacotes de serviços</summary>
          <QueryState query={packages}>
            {(data) =>
              data.length ? (
                <ul className={s.recordList}>
                  {data.map((item) => (
                    <li key={item.id}>
                      {item.name} · {item.sessionsIncluded} sessões · {item.validityDays} dias ·{" "}
                      {fmtMoney(item.price)}
                    </li>
                  ))}
                </ul>
              ) : (
                <p>Nenhum pacote disponível.</p>
              )
            }
          </QueryState>
          {manageCatalog && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) =>
                    api.createPackage({
                      name: field(data, "name"),
                      price: numberField(data, "price"),
                      sessionsIncluded: numberField(data, "sessionsIncluded"),
                      validityDays: numberField(data, "validityDays"),
                    }),
                  "Pacote criado.",
                )
              }
            >
              <TextField label="Nome do pacote" name="name" maxLength={150} required />
              <TextField
                label="Preço (R$)"
                name="price"
                type="number"
                min="0.01"
                max="100000"
                step="0.01"
                required
              />
              <TextField
                label="Sessões inclusas"
                name="sessionsIncluded"
                type="number"
                min="1"
                max="1000"
                required
              />
              <TextField
                label="Validade em dias"
                name="validityDays"
                type="number"
                min="1"
                max="3650"
                required
              />
              <Button type="submit" loading={op.busy}>
                Criar pacote
              </Button>
            </form>
          )}
        </details>
      )}
      {viewCatalog && (
        <details>
          <summary>Clubes e adesões</summary>
          <QueryState query={plans}>
            {(data) =>
              data.length ? (
                <ul className={s.recordList}>
                  {data.map((plan) => (
                    <li key={plan.id}>
                      {plan.name} · {fmtMoney(plan.monthlyPrice)}/mês
                    </li>
                  ))}
                </ul>
              ) : (
                <p>Nenhum clube disponível.</p>
              )
            }
          </QueryState>
          {manageCatalog && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) => {
                    const discount = field(data, "discountPercentage");
                    return api.createMembershipPlan({
                      name: field(data, "name"),
                      monthlyPrice: numberField(data, "monthlyPrice"),
                      benefits: discount ? [{ discountPercentage: Number(discount) }] : [],
                    });
                  },
                  "Clube cadastrado.",
                )
              }
            >
              <TextField label="Nome do clube" name="name" maxLength={150} required />
              <TextField
                label="Mensalidade (R$)"
                name="monthlyPrice"
                type="number"
                min="0.01"
                max="100000"
                step="0.01"
                required
              />
              <TextField
                label="Desconto geral (%)"
                name="discountPercentage"
                type="number"
                min="1"
                max="100"
              />
              <Button type="submit" loading={op.busy}>
                Criar clube
              </Button>
            </form>
          )}
          {manageCustomers && !!plans.data?.length && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) =>
                    api.subscribeCustomer({
                      customerId: field(data, "customerId"),
                      membershipPlanId: field(data, "membershipPlanId"),
                      periodStart: field(data, "periodStart"),
                      periodEnd: field(data, "periodEnd"),
                    }),
                  "Adesão ao clube registrada.",
                )
              }
            >
              <TextField name="customerId" label="ID do cliente" pattern={tenantPattern} required />
              <RecordSelect name="membershipPlanId" label="Clube" records={plans.data} required />
              <TextField type="date" name="periodStart" label="Início do período" required />
              <TextField type="date" name="periodEnd" label="Fim do período" required />
              <Button type="submit" loading={op.busy}>
                Registrar adesão
              </Button>
            </form>
          )}
        </details>
      )}
      {viewCustomers && (
        <details>
          <summary>Fidelidade</summary>
          <form
            className={s.formGrid}
            onSubmit={(event) => {
              event.preventDefault();
              setCustomer(field(new FormData(event.currentTarget), "id"));
            }}
          >
            <TextField label="ID do cliente" name="id" pattern={tenantPattern} required />
            <Button type="submit" variant="subtle">
              Consultar pontos
            </Button>
          </form>
          {customerId && (
            <QueryState query={loyalty}>
              {(account) => <p>{account.points.toLocaleString("pt-BR")} pontos disponíveis.</p>}
            </QueryState>
          )}
          {customerId && manageCustomers && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) => api.addPoints(customerId, numberField(data, "points")),
                  "Pontos adicionados.",
                )
              }
            >
              <TextField
                label="Pontos a creditar"
                name="points"
                type="number"
                min="1"
                max="100000"
                required
              />
              <Button type="submit" loading={op.busy}>
                Creditar pontos
              </Button>
            </form>
          )}
        </details>
      )}
      {(manageCustomers || viewReports) && (
        <details>
          <summary>Avaliações e NPS</summary>
          {viewReports && (
            <>
              <div className={s.formGrid}>
                <TextField
                  label="Data inicial"
                  name="nps-from"
                  type="date"
                  value={from}
                  onChange={(event) => setFrom(event.target.value)}
                />
                <TextField
                  label="Data final"
                  name="nps-to"
                  type="date"
                  value={to}
                  onChange={(event) => setTo(event.target.value)}
                />
              </div>
              {from && to && from <= to && (
                <QueryState query={nps}>
                  {(data) => (
                    <p>
                      NPS {data.score} · {data.totalResponses} respostas
                    </p>
                  )}
                </QueryState>
              )}
            </>
          )}
          {manageCustomers && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) =>
                    api.review({
                      customerId: field(data, "customerId"),
                      appointmentId: field(data, "appointmentId"),
                      score: numberField(data, "score"),
                      ...optional(data, "comment"),
                    }),
                  "Avaliação registrada.",
                )
              }
            >
              <TextField name="customerId" label="ID do cliente" pattern={tenantPattern} required />
              <TextField
                name="appointmentId"
                label="ID do atendimento"
                pattern={tenantPattern}
                required
              />
              <TextField name="score" label="Nota" type="number" min="0" max="10" required />
              <TextField name="comment" label="Comentário" maxLength={1000} />
              <Button type="submit" loading={op.busy}>
                Registrar avaliação
              </Button>
            </form>
          )}
        </details>
      )}
    </ModuleCard>
  );
}

export function WhatsAppModule() {
  const { user } = useAuth();
  const viewCustomers = hasPermission(user, "ViewCustomers");
  const manageCustomers = hasPermission(user, "ManageCustomers");
  const viewSettings = hasPermission(user, "ViewTenantSettings");
  const manageSettings = hasPermission(user, "ManageTenantSettings");
  const [conversationsPage, setConversationsPage] = useState(1);
  const [notificationsPage, setNotificationsPage] = useState(1);
  const [id, setId] = useState("");
  const conversations = useQuery({
    queryKey: ["panel", "whatsapp-conversations", conversationsPage],
    queryFn: () => api.conversations(conversationsPage),
    enabled: viewCustomers,
  });
  const conversation = useQuery({
    queryKey: ["panel", "whatsapp-conversation", id],
    queryFn: () => api.conversation(id),
    enabled: viewCustomers && !!id,
  });
  const notifications = useQuery({
    queryKey: ["panel", "whatsapp-notifications", notificationsPage],
    queryFn: () => api.notifications(notificationsPage),
    enabled: viewCustomers,
  });
  const policy = useQuery({
    queryKey: ["panel", "whatsapp-policy"],
    queryFn: api.policy,
    enabled: viewSettings,
  });
  const templates = useQuery({
    queryKey: ["panel", "whatsapp-templates"],
    queryFn: api.templates,
    enabled: viewSettings,
  });
  const op = useOperation();
  if (!viewCustomers && !viewSettings) return null;
  return (
    <ModuleCard title="WhatsApp">
      <Alert>O envio depende da configuração da Meta no servidor.</Alert>
      {op.feedback}
      {viewCustomers && (
        <>
          <details>
            <summary>Conversas</summary>
            <QueryState query={conversations}>
              {(data) => (
                <>
                  {data.items.length ? (
                    <ul className={s.recordList}>
                      {data.items.map((item) => (
                        <li key={item.id}>
                          {item.phoneNumber} · {item.status} · {item.messageCount} mensagens
                          <Button variant="ghost" onClick={() => setId(item.id)}>
                            Abrir conversa
                          </Button>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p>Nenhuma conversa registrada.</p>
                  )}
                  <Pagination
                    data={data}
                    setPage={(page) => {
                      setConversationsPage(page);
                      setId("");
                    }}
                  />
                </>
              )}
            </QueryState>
            {id && (
              <QueryState query={conversation}>
                {(data) => (
                  <>
                    <h3>Conversa com {data.phoneNumber}</h3>
                    <div className={s.messageList}>
                      {data.messages.map((message) => (
                        <article key={message.id} className={s.message}>
                          <strong>
                            {message.direction} · {fmtDateTime(message.sentAt)}
                          </strong>
                          <p>{message.body}</p>
                        </article>
                      ))}
                    </div>
                    {manageCustomers && (
                      <form
                        className={s.formGrid}
                        onSubmit={(event) =>
                          op.submit(
                            event,
                            (form) => api.escalate(id, field(form, "userId")),
                            "Conversa encaminhada.",
                          )
                        }
                      >
                        <TextField
                          label="ID do atendente"
                          name="userId"
                          pattern={tenantPattern}
                          required
                        />
                        <Button type="submit" loading={op.busy}>
                          Encaminhar conversa
                        </Button>
                      </form>
                    )}
                  </>
                )}
              </QueryState>
            )}
          </details>
          <details>
            <summary>Notificações agendadas</summary>
            <QueryState query={notifications}>
              {(data) => (
                <>
                  {data.items.length ? (
                    <ul className={s.recordList}>
                      {data.items.map((item) => (
                        <li key={item.id}>
                          {item.recipient} · {item.messageType} · {fmtDateTime(item.scheduledAt)}
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p>Nenhuma notificação agendada.</p>
                  )}
                  <Pagination data={data} setPage={setNotificationsPage} />
                </>
              )}
            </QueryState>
          </details>
        </>
      )}
      {viewSettings && (
        <details>
          <summary>Modelos de mensagens e política</summary>
          <QueryState query={policy}>
            {(data) => (
              <p>
                {data.allowedTypes.length
                  ? `Tipos permitidos: ${data.allowedTypes.join(", ")}`
                  : "Nenhum tipo de mensagem permitido."}
              </p>
            )}
          </QueryState>
          <QueryState query={templates}>
            {(data) =>
              data.length ? (
                <ul className={s.recordList}>
                  {data.map((item) => (
                    <li key={item.id}>
                      <div>
                        <strong>{item.key}</strong> · {item.messageType}
                        <p>{item.body}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p>Nenhum modelo cadastrado.</p>
              )
            }
          </QueryState>
          {manageSettings && !!policy.data?.allowedTypes.length && (
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) => {
                    const type = field(data, "messageType");
                    return api.createTemplate(
                      field(data, "key"),
                      /^\d+$/.test(type) ? Number(type) : type,
                      field(data, "body"),
                    );
                  },
                  "Modelo cadastrado.",
                )
              }
            >
              <TextField label="Nome de referência" name="key" maxLength={100} required />
              <RecordSelect
                label="Tipo de mensagem"
                name="messageType"
                records={policy.data.allowedTypes.map((type) => ({
                  id: String(type),
                  name: String(type),
                }))}
                required
              />
              <TextField label="Conteúdo da mensagem" name="body" maxLength={2000} required />
              <Button type="submit" loading={op.busy}>
                Criar modelo
              </Button>
            </form>
          )}
        </details>
      )}
    </ModuleCard>
  );
}
