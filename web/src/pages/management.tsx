/**
 * Páginas de site, assinatura e financeiro com escrita (F18.6, F18.7, F18.5).
 */

import { useCallback, useEffect, useState } from 'react';
import type { ApiClient } from '../api/client';
import type { FinancialReport, Plan, Site, Subscription } from '../api/types';
import {
  Field,
  FormError,
  FormPanel,
  FormSuccess,
  SubmitButton,
  useSubmit,
} from '../components/forms';
import { EmptyState, ErrorMessage, Loading, PageHeader } from './index';

// ---------- F18.6: site ----------

export function SitePage({ client }: { client: ApiClient }) {
  const [site, setSite] = useState<Site | null>(null);
  const [loading, setLoading] = useState(true);
  const [isFormOpen, setFormOpen] = useState(false);
  const [subdomain, setSubdomain] = useState('');

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const result = await client.request<Site>('GET', '/api/v1/site');
      setSite(result);
    } catch {
      // Site ainda nao criado: nao e erro, e estado inicial.
      setSite(null);
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleCreate(event: React.FormEvent): Promise<void> {
    event.preventDefault();

    const created = await form.submit(
      async () => {
        await client.request<Site>('POST', '/api/v1/site', { subdomain });
      },
      'Site criado.',
    );

    if (created) {
      setFormOpen(false);
      await load();
    }
  }

  async function handlePublish(): Promise<void> {
    const done = await form.submit(
      async () => {
        await client.request<{ id: string }>('POST', '/api/v1/site/publish');
      },
      'Site publicado.',
    );

    if (done) {
      await load();
    }
  }

  return (
    <>
      <PageHeader title="Site" subtitle="Presença online do estabelecimento" />

      {loading && <Loading />}

      {!loading && site === null && (
        <>
          <EmptyState message="O site ainda não foi criado." />

          <FormPanel
            title="Criar site"
            isOpen={isFormOpen}
            onToggle={() => setFormOpen(!isFormOpen)}
          >
            <form onSubmit={(event) => void handleCreate(event)}>
              <Field
                id="site-subdomain"
                label="Endereço"
                value={subdomain}
                onChange={setSubdomain}
                required
                placeholder="minha-barbearia"
                help="Letras minúsculas, números e hífen. Será usado como subdomínio."
              />

              <FormError message={form.error} />
              <SubmitButton isSubmitting={form.isSubmitting} label="Criar site" />
            </form>
          </FormPanel>
        </>
      )}

      {site !== null && (
        <>
          <dl className="metrics">
            <dt>Endereço</dt>
            <dd>{site.subdomain}.servia.app</dd>
            <dt>Situação</dt>
            <dd>{site.isPublished ? 'Publicado' : 'Rascunho'}</dd>
            <dt>Páginas</dt>
            <dd>{site.pageCount}</dd>
            <dt>Versões publicadas</dt>
            <dd>{site.publicationCount}</dd>
          </dl>

          <FormError message={form.error} />
          <FormSuccess message={form.success} />

          <button
            type="button"
            className="btn-primary"
            onClick={() => void handlePublish()}
            disabled={form.isSubmitting}
          >
            {form.isSubmitting ? 'Publicando…' : 'Publicar site'}
          </button>
        </>
      )}
    </>
  );
}

// ---------- F18.7: assinatura ----------

export function SubscriptionPage({ client }: { client: ApiClient }) {
  const [subscription, setSubscription] = useState<Subscription | null>(null);
  const [plans, setPlans] = useState<readonly Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const availablePlans = await client.request<readonly Plan[]>('GET', '/api/v1/billing/plans');
      setPlans(availablePlans);

      try {
        const current = await client.request<Subscription>('GET', '/api/v1/billing/subscription');
        setSubscription(current);
      } catch {
        // Sem assinatura: o tenant ainda nao contratou.
        setSubscription(null);
      }

      setLoadError(null);
    } catch (exception) {
      setLoadError(exception instanceof Error ? exception.message : 'Falha ao carregar a assinatura.');
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleChoose(planCode: string): Promise<void> {
    const method = subscription === null ? 'POST' : 'PATCH';

    const done = await form.submit(
      async () => {
        await client.request<Subscription>(method, '/api/v1/billing/subscription', { planCode });
      },
      subscription === null ? 'Plano contratado.' : 'Plano alterado.',
    );

    if (done) {
      await load();
    }
  }

  async function handleCancel(): Promise<void> {
    const done = await form.submit(
      async () => {
        await client.request<Subscription>('POST', '/api/v1/billing/subscription/cancel');
      },
      'Assinatura cancelada.',
    );

    if (done) {
      await load();
    }
  }

  return (
    <>
      <PageHeader title="Assinatura" subtitle="Plano contratado" />

      {loading && <Loading />}
      {loadError !== null && <ErrorMessage message={loadError} />}

      {!loading && subscription !== null && (
        <dl className="metrics">
          <dt>Plano</dt>
          <dd>{subscription.planName}</dd>
          <dt>Situação</dt>
          <dd>{subscription.status}</dd>
          <dt>Período</dt>
          <dd>
            {subscription.periodStart} a {subscription.periodEnd}
          </dd>
          {subscription.trialEndsAt !== null && (
            <>
              <dt>Trial até</dt>
              <dd>{subscription.trialEndsAt}</dd>
            </>
          )}
        </dl>
      )}

      <FormError message={form.error} />
      <FormSuccess message={form.success} />

      {!loading && plans.length > 0 && (
        <section>
          <h2>{subscription === null ? 'Escolha um plano' : 'Trocar de plano'}</h2>

          <ul className="plan-list">
            {plans.map((plan) => {
              const isCurrent = subscription?.planCode === plan.code;

              return (
                <li key={plan.code} className={isCurrent ? 'plan-card current' : 'plan-card'}>
                  <h3>{plan.name}</h3>
                  <p className="plan-price">R$ {plan.monthlyPrice.toFixed(2)}/mês</p>
                  <p className="muted">
                    {plan.includedProfessionals === 0
                      ? 'Profissionais ilimitados'
                      : `Até ${plan.includedProfessionals} profissionais`}
                  </p>
                  <p className="muted">Personalização: {plan.customizationLevel}</p>

                  {isCurrent ? (
                    <span className="badge">Plano atual</span>
                  ) : (
                    <button
                      type="button"
                      className="btn-primary"
                      onClick={() => void handleChoose(plan.code)}
                      disabled={form.isSubmitting}
                    >
                      {subscription === null ? 'Contratar' : 'Escolher'}
                    </button>
                  )}
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {subscription !== null && subscription.status !== 'Cancelled' && (
        <button
          type="button"
          className="btn-secondary"
          onClick={() => void handleCancel()}
          disabled={form.isSubmitting}
        >
          Cancelar assinatura
        </button>
      )}
    </>
  );
}

// ---------- F18.5: financeiro ----------

export function FinancePage({ client }: { client: ApiClient }) {
  const [report, setReport] = useState<FinancialReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isFormOpen, setFormOpen] = useState(false);

  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [type, setType] = useState('Income');

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    const today = new Date();
    const from = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10);
    const to = today.toISOString().slice(0, 10);

    try {
      const result = await client.request<FinancialReport>(
        'GET',
        `/api/v1/finance/report?from=${from}&to=${to}`,
      );

      setReport(result);
      setLoadError(null);
    } catch (exception) {
      setLoadError(exception instanceof Error ? exception.message : 'Falha ao carregar o relatório.');
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleCreate(event: React.FormEvent): Promise<void> {
    event.preventDefault();

    const created = await form.submit(
      async () => {
        await client.request<{ id: string }>('POST', '/api/v1/finance/transactions', {
          type,
          amount: Number(amount),
          description,
        });
      },
      'Lançamento registrado.',
    );

    if (created) {
      setAmount('');
      setDescription('');
      setFormOpen(false);
      await load();
    }
  }

  return (
    <>
      <PageHeader title="Financeiro" subtitle="Resultado do mês corrente" />

      <FormPanel
        title="Novo lançamento"
        isOpen={isFormOpen}
        onToggle={() => setFormOpen(!isFormOpen)}
      >
        <form onSubmit={(event) => void handleCreate(event)}>
          <div className="field">
            <label htmlFor="tx-type">Tipo</label>
            <select id="tx-type" value={type} onChange={(event) => setType(event.target.value)}>
              <option value="Income">Receita</option>
              <option value="Expense">Despesa</option>
            </select>
          </div>

          <Field
            id="tx-amount"
            label="Valor (R$)"
            type="number"
            value={amount}
            onChange={setAmount}
            required
            min={0.01}
            step="0.01"
          />
          <Field
            id="tx-description"
            label="Descrição"
            value={description}
            onChange={setDescription}
            required
          />

          <FormError message={form.error} />
          <SubmitButton isSubmitting={form.isSubmitting} label="Registrar" />
        </form>
      </FormPanel>

      <FormSuccess message={form.success} />

      {loading && <Loading />}
      {loadError !== null && <ErrorMessage message={loadError} />}

      {report !== null && (
        <dl className="metrics">
          <dt>Receitas</dt>
          <dd>R$ {report.totalIncome.toFixed(2)}</dd>
          <dt>Despesas</dt>
          <dd>R$ {report.totalExpense.toFixed(2)}</dd>
          <dt>Saldo</dt>
          <dd>R$ {report.balance.toFixed(2)}</dd>
          <dt>Ticket médio</dt>
          <dd>R$ {report.averageTicket.toFixed(2)}</dd>
          <dt>Atendimentos</dt>
          <dd>{report.appointmentCount}</dd>
        </dl>
      )}
    </>
  );
}
