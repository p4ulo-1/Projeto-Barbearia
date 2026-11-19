import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button } from "@/components/ui-sv";
import { ApiError } from "@/lib/api/http";
import { operations as api, type Checkout, type Subscription } from "@/lib/api/operations";
import { operationErrorMessage } from "@/lib/operation-feedback";
import { CheckoutCard } from "./CheckoutCard";
import { QueryState } from "./OperationsUI";
import { fmtDateTime } from "./status";

const messages: Record<string, string> = {
  AwaitingExternalConfirmation: "Aguardando confirmação da autorização pelo provedor.",
  RequiresReconciliation:
    "A confirmação está pendente de verificação. Não inicie outra contratação.",
  Trialing: "Autorização confirmada; trial ativo.",
  AwaitingFinancialConfirmation:
    "Primeira cobrança aguardando confirmação. O plano pago ainda não foi ativado.",
  FirstPaymentRejected: "A primeira cobrança foi recusada. Aguardando regularização.",
  Active: "Pagamento confirmado e assinatura ativa.",
  Failed: "A contratação não foi concluída.",
  CapacityExceeded: "O plano selecionado não comporta a equipe elegível.",
  CancellationPending: "Cancelamento aguardando confirmação.",
  CancellationScheduled: "Cancelamento confirmado para o fim do período pago.",
};
export function CheckoutFlow({
  subscription,
  selectedPlan,
}: {
  subscription: Subscription | null;
  selectedPlan: string;
}) {
  const qc = useQueryClient();
  const inFlightKey = useRef<string | null>(null);
  const submissionLock = useRef(false);
  const [result, setResult] = useState<Checkout | null>(null);
  const [error, setError] = useState("");
  const [uncertain, setUncertain] = useState(false);
  const current = useQuery({
    queryKey: ["panel", "checkout-current"],
    queryFn: api.currentCheckout,
    retry: false,
  });
  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ["panel"] });
  };
  const start = useMutation({
    mutationFn: async (token: string) => {
      const key = inFlightKey.current ?? crypto.randomUUID();
      inFlightKey.current = key;
      return api.startCheckout(key, selectedPlan, token);
    },
    onSuccess: async (data) => {
      setResult(data);
      setError("");
      await refresh();
    },
    onError: async (failure) => {
      setUncertain(true);
      setError(
        failure instanceof ApiError && failure.status === 503
          ? "A contratação está temporariamente indisponível. Consulte a tentativa antes de tentar novamente."
          : failure instanceof ApiError && failure.status !== 0
            ? `${operationErrorMessage(failure)} Consulte a tentativa antes de tentar novamente.`
            : "Não foi possível confirmar o início. Consulte a tentativa antes de tentar novamente.",
      );
      await refresh();
    },
  });
  const verify = useMutation({
    mutationFn: (key: string) => api.verifyCheckout(key),
    onSuccess: async (data) => {
      setResult(data);
      setError("");
      await refresh();
    },
    onError: (failure) => setError(operationErrorMessage(failure)),
  });
  const attempt = result && current.data?.checkoutKey === result.checkoutKey
    ? { ...result, planCode: current.data.planCode ?? result.planCode }
    : result ?? current.data;
  const financialState = subscription?.firstPaymentStatus;
  const displayState = attempt && ["Trialing", "AwaitingFinancialConfirmation", "FirstPaymentRejected", "Active"].includes(attempt.state)
    ? ({ Scheduled: "Trialing", Pending: "AwaitingFinancialConfirmation", Rejected: "FirstPaymentRejected", Paid: "Active" } as Record<string, string>)[financialState ?? ""] ?? attempt.state
    : attempt?.state;
  const startAllowed =
    !attempt &&
    !uncertain &&
    !start.isPending &&
    !current.isError &&
    !current.isPending &&
    !!selectedPlan;
  const isNewTrial = subscription?.status === "AwaitingTrialAuthorization";
  async function begin(token: string) {
    if (submissionLock.current || !startAllowed) return;
    submissionLock.current = true;
    try {
      await start.mutateAsync(token);
    } catch {
      /* feedback is displayed by the mutation */
    } finally {
      submissionLock.current = false;
    }
  }
  return (
    <section>
      <h3>Contratação recorrente</h3>
      <p>
        O trial de 14 dias começa após a confirmação da autorização do cartão. A primeira cobrança é
        programada para o fim do trial.
      </p>
      {error && <Alert tone="error">{error}</Alert>}
      {uncertain && !attempt && (
        <Button type="button" variant="subtle" onClick={() => void current.refetch()}>
          Consultar tentativa pendente
        </Button>
      )}
      <QueryState query={current}>
        {() => (
          <>
            {attempt && (
              <>
                <p>
                  {messages[displayState ?? attempt.state] ??
                    "A contratação está em verificação. Consulte novamente o estado."}
                </p>
                {attempt.planCode && <p>Plano da tentativa: {attempt.planCode}.</p>}
                {displayState === "Trialing" && attempt.trialStartsAt && attempt.trialEndsAt && (
                  <p>
                    Trial confirmado: {fmtDateTime(attempt.trialStartsAt)} até{" "}
                    {fmtDateTime(attempt.trialEndsAt)}. Primeira cobrança prevista para o
                    vencimento.
                  </p>
                )}
                <Button
                  type="button"
                  variant="subtle"
                  loading={verify.isPending}
                  disabled={start.isPending}
                  onClick={() => verify.mutate(attempt.checkoutKey)}
                >
                  Consultar contratação
                </Button>
                {attempt.state === "Failed" && !current.data && (
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => {
                      setResult(null);
                      setUncertain(false);
                      inFlightKey.current = null;
                    }}
                  >
                    Escolher novamente
                  </Button>
                )}
              </>
            )}
            {!attempt &&
              isNewTrial &&
              (selectedPlan ? (
                <CheckoutCard busy={!startAllowed} onToken={begin} />
              ) : (
                <p>Selecione um plano para autorizar o cartão.</p>
              ))}
            {!attempt && !isNewTrial && (
              <p>
                A contratação de um novo plano não está disponível para o estado atual da
                assinatura.
              </p>
            )}
          </>
        )}
      </QueryState>
      <p>
        Mudanças de plano aguardam confirmação financeira; upgrade e downgrade ainda não estão
        disponíveis nesta tela.
      </p>
    </section>
  );
}
