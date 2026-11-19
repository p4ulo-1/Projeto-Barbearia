import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button } from "@/components/ui-sv";
import {
  operations as api,
  type PlanChangeOperation,
  type Subscription,
} from "@/lib/api/operations";
import { operationErrorMessage } from "@/lib/operation-feedback";
import { UpgradePaymentCard } from "./CheckoutCard";
import { QueryState } from "./OperationsUI";
import { fmtDateTime, fmtMoney } from "./status";
import s from "./styles";

const labels: Record<string, string> = {
  AwaitingPaymentConsent: "Autorize a cobrança proporcional.",
  AwaitingPaymentConfirmation: "Aguardando confirmação da cobrança adicional.",
  AwaitingRecurringConfirmation: "Aguardando confirmação do novo valor recorrente.",
  AwaitingCycleConfirmation:
    "Downgrade programado. O plano vigente continua até a confirmação do próximo ciclo.",
  RequiresReconciliation: "A operação está em verificação. Acompanhe por esta página.",
  Effective: "Mudança de plano confirmada.",
  Failed: "A mudança de plano foi encerrada.",
};

const issues: Record<string, string> = {
  CapacityExceeded: "A equipe elegível excede a capacidade do plano solicitado.",
  RecurringTermsUnconfirmed: "O provedor ainda não confirmou valor e vencimento para este ciclo.",
  RecurringDateMismatch: "O vencimento apresentado pelo provedor diverge do ciclo registrado.",
  RecurringAuthorizationUnconfirmed: "O provedor ainda não confirmou a autorização da recorrência.",
  CycleInvoiceAlreadyIssued:
    "A cobrança do próximo ciclo já foi emitida ou seu valor ainda está em verificação.",
  CycleChargedAtOriginalPrice:
    "O ciclo foi cobrado pelo preço original. O plano vigente foi preservado.",
  CycleChanged: "O período financeiro da solicitação mudou.",
  PaymentRejected: "O pagamento adicional foi recusado.",
  PaymentMismatch: "O pagamento adicional precisa de verificação.",
  QuoteExpired: "A cotação expirou. Solicite uma nova cotação para continuar.",
  CancellationRequested: "A assinatura entrou em cancelamento.",
};

export function PlanChangeFlow({
  subscription,
  selectedPlan,
  downgrade,
}: {
  subscription: Subscription;
  selectedPlan: string;
  downgrade: boolean;
}) {
  const qc = useQueryClient();
  const key = ["panel", "plan-change", subscription.id];
  const requestKey = useRef<{ plan: string; id: string } | null>(null);
  const requesting = useRef(false);
  const [review, setReview] = useState(false);
  const operation = useQuery({ queryKey: key, queryFn: () => api.planChangeOperation() });
  async function refresh(result?: PlanChangeOperation) {
    if (result) qc.setQueryData(key, result);
    await qc.invalidateQueries({ queryKey: ["panel"] });
  }
  const request = useMutation({
    mutationFn: async (plan: string) => {
      if (requestKey.current?.plan !== plan) requestKey.current = { plan, id: crypto.randomUUID() };
      return api.requestPlanChange(plan, requestKey.current.id);
    },
    onSuccess: async (result) => {
      setReview(false);
      await refresh(result);
    },
    onError: () => refresh(),
    onSettled: () => {
      requesting.current = false;
    },
  });
  const payment = useMutation({
    mutationFn: ({ id, token, method }: { id: string; token: string; method: string }) =>
      api.confirmPlanChangePayment(id, token, method),
    onSuccess: (result) => refresh(result),
    onError: () => refresh(),
  });
  const verify = useMutation({ mutationFn: api.verifySubscription, onSettled: () => refresh() });
  const available =
    (subscription.status === "Active" || subscription.status === "Trialing") &&
    !subscription.cancellationRequested &&
    subscription.billingAccess === "Operational";
  const targetSelected = selectedPlan && selectedPlan !== subscription.planCode;
  return (
    <section className={s.formGrid} aria-label="Mudança de plano">
      <QueryState query={operation}>
        {(data) => {
          const expired =
            data?.state === "AwaitingPaymentConsent" &&
            !!data.quoteExpiresAt &&
            Date.parse(data.quoteExpiresAt) <= Date.now();
          const outstanding = !!data && !expired && !["Effective", "Failed"].includes(data.state);
          return (
            <>
              {data && (
                <div className={s.formGrid}>
                  <strong>
                    {data.reason === "Downgrade" ? "Downgrade" : "Upgrade"} · {data.targetPlanCode}
                  </strong>
                  <p>
                    {expired
                      ? issues["QuoteExpired"]
                      : (labels[data.state] ?? "Aguardando atualização.")}
                  </p>
                  {data.issue && (
                    <Alert tone="error">
                      {issues[data.issue] ?? "A operação requer verificação."}
                    </Alert>
                  )}
                  {data.sourcePrice != null && data.targetPrice != null && (
                    <p>
                      Recorrência de {fmtMoney(data.sourcePrice)} para {fmtMoney(data.targetPrice)}{" "}
                      por mês.
                    </p>
                  )}
                  {data.cycleEnd && (
                    <p>
                      {data.reason === "Downgrade" ? "Data prevista" : "Vencimento preservado"} ·{" "}
                      {fmtDateTime(data.cycleEnd)}.
                    </p>
                  )}
                  {data.reason === "Downgrade" && (
                    <p>Sem reembolso automático. A capacidade da equipe será revalidada.</p>
                  )}
                  {data.additionalAmount != null && (
                    <p>
                      Diferença proporcional · {fmtMoney(data.additionalAmount)}
                      {data.paymentApproved ? " · Pagamento confirmado" : ""}.
                    </p>
                  )}
                  {data.recurrenceConfirmed && (
                    <p>Novo valor recorrente confirmado pelo provedor.</p>
                  )}
                  {data.state === "AwaitingPaymentConsent" &&
                    !expired &&
                    available &&
                    data.additionalAmount != null &&
                    data.additionalAmount > 0 && (
                      <>
                        <p>
                          Ao pagar, você autoriza este débito único e a nova mensalidade na data
                          indicada.
                        </p>
                        <UpgradePaymentCard
                          amount={data.additionalAmount}
                          onPayment={async (token, method) => {
                            await payment.mutateAsync({ id: data.id, token, method });
                          }}
                        />
                      </>
                    )}
                  {outstanding && data.state !== "AwaitingPaymentConsent" && (
                    <Button
                      variant="subtle"
                      loading={verify.isPending}
                      onClick={() => verify.mutate()}
                    >
                      Verificar mudança de plano
                    </Button>
                  )}
                </div>
              )}
              {available &&
                targetSelected &&
                !outstanding &&
                !(subscription.status === "Trialing" && downgrade) && (
                  <>
                    {review && (
                      <p>
                        {downgrade
                          ? "O plano atual continua neste ciclo. A redução depende da capacidade e da confirmação no próximo ciclo."
                          : subscription.status === "Trialing"
                            ? "O upgrade mantém o vencimento original do trial e a cobrança acontecerá nessa data."
                            : "Confira a diferença proporcional calculada pelo Servia antes de autorizar o pagamento."}
                      </p>
                    )}
                    <Button
                      loading={request.isPending}
                      onClick={() => {
                        if (!review) {
                          if (expired || data?.state === "Failed") requestKey.current = null;
                          setReview(true);
                        } else if (!requesting.current) {
                          requesting.current = true;
                          request.mutate(selectedPlan);
                        }
                      }}
                    >
                      {review
                        ? downgrade
                          ? "Confirmar programação"
                          : "Confirmar solicitação"
                        : "Revisar mudança de plano"}
                    </Button>
                  </>
                )}
              {(request.error || payment.error || verify.error) && (
                <Alert tone="error">
                  {operationErrorMessage(request.error ?? payment.error ?? verify.error)}
                </Alert>
              )}
            </>
          );
        }}
      </QueryState>
    </section>
  );
}
