import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@/components/ui-sv";
import { operations as api } from "@/lib/api/operations";
import { IS_DEMO } from "@/lib/api/http";
import { tenantPattern } from "@/lib/api/contracts";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, Pagination, RecordSelect } from "./OperationsUI";
import { field, localDate, numberField, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import { fmtMoney } from "./status";
import s from "./styles";

export function FinancePage() {
  return (
    <>
      <PageHeader
        title="Financeiro"
        description="Acompanhe as receitas, despesas e pagamentos do estabelecimento."
      />
      {IS_DEMO ? (
        <Alert>
          Dados financeiros e operações de caixa ficam disponíveis ao conectar a API do Servia.
        </Alert>
      ) : (
        <FinanceDetails />
      )}
    </>
  );
}

function FinanceDetails() {
  const { user } = useAuth();
  const read = hasPermission(user, "ViewFinancialReports");
  const write = hasPermission(user, "ManageFinancialEntries");
  const [from, setFrom] = useState(() => localDate().slice(0, 7) + "-01");
  const [to, setTo] = useState(localDate);
  const [page, setPage] = useState(1);
  const [professional, setProfessional] = useState("");
  const [paymentId, setPaymentId] = useState("");
  const validRange = !!from && !!to && from <= to;
  const report = useQuery({
    queryKey: ["panel", "finance-report", from, to],
    queryFn: () => api.report(from, to),
    enabled: read && validRange,
  });
  const transactions = useQuery({
    queryKey: ["panel", "transactions", page, from, to],
    queryFn: () => api.transactions(page, from, to),
    enabled: read && validRange,
  });
  const pros = useQuery({
    queryKey: ["panel", "professionals", "options"],
    queryFn: () => api.professionals(1, 100),
    enabled: read && hasPermission(user, "ViewTeamAgenda"),
  });
  const commission = useQuery({
    queryKey: ["panel", "commissions", professional, from, to],
    queryFn: () => api.commissions(professional, from, to),
    enabled: validRange && read && !!professional,
  });
  const op = useOperation();

  return (
    <>
      {op.feedback}
      {read && (
        <ModuleCard title="Resumo do período">
          <div className={s.formGrid}>
            <TextField
              label="Data inicial"
              name="from"
              type="date"
              value={from}
              onChange={(event) => {
                setFrom(event.target.value);
                setPage(1);
              }}
            />
            <TextField
              label="Data final"
              name="to"
              type="date"
              value={to}
              onChange={(event) => {
                setTo(event.target.value);
                setPage(1);
              }}
            />
          </div>
          {validRange ? (
            <QueryState query={report} loading="summary">
              {(data) => (
                <dl className={s.stats}>
                  {[
                    ["Receitas", data.totalIncome],
                    ["Despesas", data.totalExpense],
                    ["Saldo", data.balance],
                    ["Ticket médio", data.averageTicket],
                  ].map(([label, amount]) => (
                    <div className={s.stat} key={label}>
                      <dt className={s.statLabel}>{label}</dt>
                      <dd className={s.statValue}>{fmtMoney(Number(amount))}</dd>
                    </div>
                  ))}
                </dl>
              )}
            </QueryState>
          ) : (
            <Alert>Escolha um intervalo válido para consultar o relatório.</Alert>
          )}
        </ModuleCard>
      )}
      {read && validRange && (
        <ModuleCard title="Movimentações">
          <QueryState query={transactions} loading="table">
            {(data) => (
              <>
                {data.items.length ? (
                  <div className={s.tableWrap}>
                    <table className={s.table}>
                      <thead>
                        <tr>
                          <th>Descrição</th>
                          <th>Tipo</th>
                          <th>Valor</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.items.map((item) => (
                          <tr key={item.id}>
                            <td>{item.description}</td>
                            <td>{item.type === "Income" ? "Receita" : "Despesa"}</td>
                            <td>{fmtMoney(item.amount)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p>Nenhuma movimentação no período.</p>
                )}
                <Pagination data={data} setPage={setPage} />
              </>
            )}
          </QueryState>
        </ModuleCard>
      )}
      {write && (
        <div className={s.moduleGrid}>
          <ModuleCard title="Lançar movimentação">
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  (data) =>
                    api.transaction({
                      type: numberField(data, "type"),
                      amount: numberField(data, "amount"),
                      description: field(data, "description"),
                    }),
                  "Movimentação registrada.",
                )
              }
            >
              <RecordSelect
                name="type"
                label="Tipo"
                records={[
                  { id: "1", name: "Receita" },
                  { id: "2", name: "Despesa" },
                ]}
                required
              />
              <TextField
                label="Valor (R$)"
                name="amount"
                type="number"
                min="0.01"
                max="1000000"
                step="0.01"
                required
              />
              <TextField name="description" label="Descrição" maxLength={300} required />
              <Button type="submit" loading={op.busy}>
                Registrar movimentação
              </Button>
            </form>
          </ModuleCard>
          <ModuleCard title="Registrar pagamento">
            <form
              className={s.formGrid}
              onSubmit={(event) =>
                op.submit(
                  event,
                  async (data) => {
                    const payment = await api.payment(
                      numberField(data, "amount"),
                      numberField(data, "method"),
                      field(data, "appointmentId") || undefined,
                    );
                    setPaymentId(payment.id);
                  },
                  "Pagamento registrado.",
                )
              }
            >
              <TextField
                name="amount"
                label="Valor (R$)"
                type="number"
                min="0.01"
                max="1000000"
                step="0.01"
                required
              />
              <RecordSelect
                name="method"
                label="Forma de pagamento"
                records={[
                  "Dinheiro",
                  "Cartão de crédito",
                  "Cartão de débito",
                  "Pix",
                  "Transferência",
                ].map((name, i) => ({ id: String(i + 1), name }))}
                required
              />
              <TextField
                name="appointmentId"
                label="ID do agendamento (opcional)"
                pattern={tenantPattern}
              />
              <Button type="submit" loading={op.busy}>
                Registrar pagamento
              </Button>
            </form>
            {paymentId && (
              <Alert>
                Identificador para referência ou estorno <code>{paymentId}</code>
              </Alert>
            )}
          </ModuleCard>
          <ModuleCard title="Estornar pagamento">
            <form
              className={s.formGrid}
              onSubmit={(event) => {
                event.preventDefault();
                const data = new FormData(event.currentTarget);
                const amount = numberField(data, "amount");
                if (window.confirm(`Confirmar estorno de ${fmtMoney(amount)} para este pagamento?`))
                  op.run(() => api.refund(field(data, "id"), amount), "Estorno registrado.");
              }}
            >
              <TextField
                name="id"
                label="ID do pagamento"
                pattern={tenantPattern}
                key={paymentId}
                defaultValue={paymentId}
                required
              />
              <TextField
                name="amount"
                label="Valor do estorno (R$)"
                type="number"
                min="0.01"
                max="1000000"
                step="0.01"
                required
              />
              <Button type="submit" variant="subtle" loading={op.busy}>
                Confirmar estorno
              </Button>
            </form>
          </ModuleCard>
        </div>
      )}
      {read && validRange && (
        <ModuleCard title="Comissões">
          {pros.data ? (
            <RecordSelect
              label="Profissional"
              name="professional"
              value={professional}
              onChange={setProfessional}
              records={pros.data.items}
            />
          ) : (
            <form
              className={s.formGrid}
              onSubmit={(event) => {
                event.preventDefault();
                setProfessional(field(new FormData(event.currentTarget), "id"));
              }}
            >
              <TextField name="id" label="ID do profissional" pattern={tenantPattern} required />
              <Button type="submit" variant="subtle">
                Consultar
              </Button>
            </form>
          )}
          {professional && (
            <QueryState query={commission}>
              {(data) => (
                <p>
                  Pendente {fmtMoney(data.pending)} · Aprovado {fmtMoney(data.approved)} · Pago{" "}
                  {fmtMoney(data.paid)} · A receber {fmtMoney(data.receivable)}
                </p>
              )}
            </QueryState>
          )}
        </ModuleCard>
      )}
    </>
  );
}
