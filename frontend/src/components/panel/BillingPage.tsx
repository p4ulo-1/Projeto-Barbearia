import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button } from "@/components/ui-sv";
import { ApiError, IS_DEMO } from "@/lib/api/http";
import { plansService } from "@/lib/api/plans";
import { operations as api } from "@/lib/api/operations";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, Pagination, RecordSelect } from "./OperationsUI";
import { displayDate, field, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import { fmtMoney } from "./status";
import s from "./styles";

export function BillingPage() {
  return (
    <>
      <PageHeader
        title="Assinatura Servia"
        description="Consulte o plano, as faturas e o uso dos recursos da plataforma."
      />
      {IS_DEMO ? (
        <Alert>Assinaturas e faturas ficam disponíveis ao conectar a API do Servia.</Alert>
      ) : (
        <BillingDetails />
      )}
    </>
  );
}

function BillingDetails() {
  const { user } = useAuth();
  const write = hasPermission(user, "ManageBilling");
  const [page, setPage] = useState(1);
  const sub = useQuery({
    queryKey: ["panel", "subscription"],
    queryFn: () =>
      api.subscription().catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
      }),
  });
  const plans = useQuery({ queryKey: ["plans"], queryFn: plansService.list });
  const invoices = useQuery({
    queryKey: ["panel", "invoices", page],
    queryFn: () => api.invoices(page),
  });
  const usage = useQuery({ queryKey: ["panel", "usage"], queryFn: api.usage });
  const history = useQuery({
    queryKey: ["panel", "subscription-history"],
    queryFn: api.planHistory,
  });
  const suggestion = useQuery({
    queryKey: ["panel", "subscription-suggestion"],
    queryFn: async () => (await api.upgradeSuggestion()) ?? null,
    enabled: !!sub.data,
  });
  const op = useOperation();

  return (
    <>
      {op.feedback}
      <div className={s.moduleGrid}>
        <ModuleCard title="Plano atual">
          <QueryState query={sub}>
            {(data) => (
              <>
                {data ? (
                  <>
                    <p>
                      <strong>{data.planName}</strong> ·{" "}
                      {(
                        {
                          Trialing: "Em teste",
                          Active: "Ativa",
                          PastDue: "Em atraso",
                          Cancelled: "Cancelada",
                        } as Record<string, string>
                      )[data.status] ?? data.status}
                    </p>
                    <p>
                      Período de {displayDate(data.periodStart)} a {displayDate(data.periodEnd)}.
                    </p>
                    {data.trialEndsAt && <p>Fim do teste em {displayDate(data.trialEndsAt)}.</p>}
                  </>
                ) : (
                  <p>Nenhuma assinatura cadastrada.</p>
                )}
                {data && suggestion.data && (
                  <Alert>
                    Para {suggestion.data.professionalCount} profissionais, o plano recomendado é{" "}
                    {suggestion.data.requiredPlanCode}.
                  </Alert>
                )}
                {write && data?.status !== "Cancelled" && (
                  <QueryState query={plans}>
                    {(catalog) => {
                      const options = catalog.filter((plan) => plan.code !== data?.planCode);
                      return options.length ? (
                        <form
                          className={s.formGrid}
                          onSubmit={(event) => {
                            event.preventDefault();
                            const code = field(new FormData(event.currentTarget), "plan");
                            const plan = options.find((item) => item.code === code);
                            if (
                              plan &&
                              window.confirm(
                                `${data ? "Trocar para" : "Contratar"} ${plan.name} por ${fmtMoney(plan.monthlyPrice)} por mês?`,
                              )
                            )
                              op.run(() => api.changePlan(code, !!data), "Plano atualizado.");
                          }}
                        >
                          <RecordSelect
                            name="plan"
                            label="Plano disponível"
                            records={options.map((plan) => ({
                              id: plan.code,
                              name: `${plan.name} · ${fmtMoney(plan.monthlyPrice)}/mês`,
                            }))}
                            required
                          />
                          <Button type="submit" loading={op.busy}>
                            {data ? "Alterar plano" : "Contratar plano"}
                          </Button>
                        </form>
                      ) : null;
                    }}
                  </QueryState>
                )}
                {write && data && data.status !== "Cancelled" && (
                  <Button
                    variant="ghost"
                    disabled={op.busy}
                    onClick={() => {
                      if (
                        window.confirm(
                          "Cancelar a assinatura Servia? O acesso permanece até o fim do período pago.",
                        )
                      )
                        op.run(api.cancelPlan, "Assinatura cancelada.");
                    }}
                  >
                    Cancelar assinatura
                  </Button>
                )}
              </>
            )}
          </QueryState>
        </ModuleCard>
        <ModuleCard title="Uso de recursos">
          <QueryState query={usage}>
            {(data) =>
              data.length ? (
                <ul className={s.recordList}>
                  {data.map((item) => (
                    <li key={item.resource}>
                      <div>
                        <strong>
                          {{ AiUnits: "Créditos de IA", WhatsAppMessages: "Mensagens de WhatsApp" }[
                            item.resource
                          ] ?? item.resource}
                        </strong>
                        <p>
                          {item.consumed.toLocaleString("pt-BR")} usados ·{" "}
                          {item.included.toLocaleString("pt-BR")} incluídos ·{" "}
                          {item.remaining.toLocaleString("pt-BR")} restantes
                        </p>
                        {item.consumedTokens != null && (
                          <p>{item.consumedTokens.toLocaleString("pt-BR")} tokens consumidos</p>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p>Nenhum consumo registrado no período.</p>
              )
            }
          </QueryState>
        </ModuleCard>
      </div>
      <ModuleCard title="Faturas da plataforma">
        <QueryState query={invoices} loading="table">
          {(data) => (
            <>
              {data.items.length ? (
                <div className={s.tableWrap}>
                  <table className={s.table}>
                    <thead>
                      <tr>
                        <th>Período</th>
                        <th>Vencimento</th>
                        <th>Situação</th>
                        <th>Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((invoice) => (
                        <tr key={invoice.id}>
                          <td>
                            {displayDate(invoice.periodStart)} a {displayDate(invoice.periodEnd)}
                          </td>
                          <td>{displayDate(invoice.dueDate)}</td>
                          <td>{invoice.status}</td>
                          <td>{fmtMoney(invoice.total)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p>Nenhuma fatura registrada.</p>
              )}
              <Pagination data={data} setPage={setPage} />
            </>
          )}
        </QueryState>
      </ModuleCard>
      <ModuleCard title="Histórico da assinatura">
        <QueryState query={history}>
          {(data) =>
            data.length ? (
              <ul className={s.recordList}>
                {data.map((item) => (
                  <li key={item.id}>
                    {(
                      {
                        Initial: "Contratação",
                        Upgrade: "Upgrade",
                        Downgrade: "Downgrade",
                      } as Record<string, string>
                    )[item.reason] ?? item.reason}{" "}
                    · {displayDate(item.changedAt)}
                  </li>
                ))}
              </ul>
            ) : (
              <p>Nenhuma alteração registrada.</p>
            )
          }
        </QueryState>
      </ModuleCard>
    </>
  );
}
