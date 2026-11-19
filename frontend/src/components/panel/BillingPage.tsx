import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button } from "@/components/ui-sv";
import { ApiError, IS_DEMO } from "@/lib/api/http";
import { planBenefits, plansService } from "@/lib/api/plans";
import { operations as api } from "@/lib/api/operations";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, Pagination, RecordSelect } from "./OperationsUI";
import { displayDate } from "./forms";
import { operationErrorMessage } from "@/lib/operation-feedback";
import { CheckoutFlow } from "./CheckoutFlow";
import { PageHeader } from "./PanelPage";
import { fmtMoney, fmtDateTime } from "./status";
import s from "./styles";

export function BillingPage() {
  return (
    <>
      <PageHeader
        title="Assinatura Servia"
        description="Consulte o plano, as faturas e o histórico da assinatura."
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
  const write = !!user?.permissions && hasPermission(user, "ManageBilling");
  const qc = useQueryClient();
  const [page, setPage] = useState(1);
  const [selectedPlan, setSelectedPlan] = useState("");
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
  const history = useQuery({
    queryKey: ["panel", "subscription-history"],
    queryFn: api.planHistory,
  });
  const suggestion = useQuery({
    queryKey: ["panel", "subscription-suggestion"],
    queryFn: async () => (await api.upgradeSuggestion()) ?? null,
    enabled: !!sub.data,
  });
  const cancel = useMutation({
    mutationFn: api.cancelPlan,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["panel"] }),
  });
  const verify = useMutation({
    mutationFn: api.verifySubscription,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["panel"] }),
  });

  return (
    <>
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
                          AwaitingTrialAuthorization: "Aguardando autorização para iniciar o trial",
                          AuthorizedAwaitingFirstPayment: "Primeira cobrança pendente",
                          PendingCheckout: "Contratação pendente",
                          PendingConfirmation: "Autorização em confirmação",
                          Expired: "Expirada",
                        } as Record<string, string>
                      )[data.status] ?? data.status}
                    </p>
                    {data.status !== "AwaitingTrialAuthorization" && (
                      <p>
                        Período de {displayDate(data.periodStart)} a {displayDate(data.periodEnd)}.
                      </p>
                    )}
                    {data.status === "Trialing" &&
                      data.trialStartedAtUtc &&
                      data.trialEndsAtUtc && (
                        <p>
                          Trial autorizado: {fmtDateTime(data.trialStartedAtUtc)} até{" "}
                          {fmtDateTime(data.trialEndsAtUtc)}. Primeira cobrança prevista para o
                          vencimento.
                        </p>
                      )}
                    {data.nextChargeAtUtc && (
                      <p>Próxima cobrança prevista: {fmtDateTime(data.nextChargeAtUtc)}.</p>
                    )}
                    {data.firstPaymentStatus && (
                      <p>
                        {({
                          Scheduled: "Primeira cobrança programada para o fim do trial.",
                          Pending: "Primeira cobrança aguardando confirmação.",
                          Rejected: "A primeira cobrança foi recusada. Aguardando regularização.",
                          Paid: "Primeira cobrança paga e confirmada.",
                        })[data.firstPaymentStatus]}
                      </p>
                    )}
                    {(data.status === "PastDue" ||
                      data.billingAccess === "NoNewBookings" ||
                      (data.status === "Active" && data.billingAccess === "OwnerReadOnly")) && (
                      <Alert tone="error">
                        Pagamento pendente. Novos agendamentos estão bloqueados.{" "}
                        {data.billingAccess === "OwnerReadOnly" &&
                          "O acesso está restrito ao proprietário, em modo de leitura."}
                      </Alert>
                    )}
                    {data.cancellationRequested && (
                      <p>
                        {data.cancellationEffectiveAtUtc
                          ? `Cancelamento confirmado para ${fmtDateTime(data.cancellationEffectiveAtUtc)}.`
                          : "Cancelamento solicitado; aguardando confirmação."}
                      </p>
                    )}
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
                {write && (
                  <QueryState query={plans}>
                    {(catalog) => {
                      const selected = catalog.find((plan) => plan.code === selectedPlan);
                      return catalog.length ? (
                        <div className={s.formGrid}>
                          <RecordSelect
                            name="plan"
                            label="Selecionar plano para revisão"
                            value={selectedPlan}
                            onChange={setSelectedPlan}
                            records={catalog.map((plan) => ({
                              id: plan.code,
                              name: `${plan.name} · ${fmtMoney(plan.monthlyPrice)}/mês`,
                            }))}
                          />
                          {selected && (
                            <div>
                              <p>
                                Opção selecionada: <strong>{selected.name}</strong>
                                {selected.code === data?.planCode ? " (plano atual)" : ""}
                              </p>
                              <p>{fmtMoney(selected.monthlyPrice)} por mês</p>
                              <ul>
                                {planBenefits(selected).map((benefit) => (
                                  <li key={benefit}>{benefit}</li>
                                ))}
                              </ul>
                            </div>
                          )}
                          <p>
                            A seleção não altera o plano vigente. A contratação depende de
                            confirmação pelo servidor.
                          </p>
                        </div>
                      ) : (
                        <p>Nenhum plano disponível no momento.</p>
                      );
                    }}
                  </QueryState>
                )}
                {write && <CheckoutFlow subscription={data} selectedPlan={selectedPlan} />}
                {write && data && (
                  <Button
                    variant="subtle"
                    loading={verify.isPending}
                    onClick={() => verify.mutate()}
                  >
                    Atualizar situação financeira
                  </Button>
                )}
                {verify.data && (
                  <p>
                    {verify.data.state === "PaymentRejected"
                      ? "Pagamento recusado; aguardando regularização."
                      : `Situação consultada: ${verify.data.status}.`}
                  </p>
                )}
                {(verify.error || cancel.error) && (
                  <Alert tone="error">{operationErrorMessage(verify.error ?? cancel.error)}</Alert>
                )}
                {cancel.data && (
                  <p>
                    {cancel.data.state === "Cancelled"
                      ? "Cancelamento confirmado."
                      : cancel.data.state === "CancellationScheduled"
                        ? "Cancelamento confirmado para o fim do período pago."
                        : "Cancelamento solicitado; aguardando confirmação."}
                  </p>
                )}
                {write && data && data.status !== "Cancelled" && !data.cancellationRequested && (
                  <Button
                    variant="ghost"
                    disabled={cancel.isPending}
                    onClick={() => {
                      if (
                        window.confirm(
                          "Solicitar o cancelamento da assinatura? Durante o trial, o encerramento depende da confirmação; o período pago será respeitado quando aplicável.",
                        )
                      )
                        cancel.mutate();
                    }}
                  >
                    Cancelar assinatura
                  </Button>
                )}
              </>
            )}
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
                        Initial: "Início da assinatura",
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
