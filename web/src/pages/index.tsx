/**
 * Páginas do painel (F14.3–F14.9).
 *
 * Cada página é uma superfície de produto. O layout é compartilhado; o
 * conteúdo muda por rota.
 */

import { useEffect, useState, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useSession } from '../auth/SessionProvider';
import type { ApiClient, PagedResponse } from '../api/client';
import type {
  Appointment,
  Customer,
  FinancialReport,
  Professional,
  Service,
  Site,
  Subscription,
} from '../api/types';

// ---------- Componentes compartilhados ----------

export function PageHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <header className="page-header">
      <h1>{title}</h1>
      {subtitle !== undefined && <p className="muted">{subtitle}</p>}
    </header>
  );
}

export function Loading() {
  return <p role="status" aria-live="polite">Carregando…</p>;
}

export function ErrorMessage({ message }: { message: string }) {
  return (
    <p role="alert" className="error">
      {message}
    </p>
  );
}

export function EmptyState({ message }: { message: string }) {
  return <p className="muted">{message}</p>;
}

/** Layout do painel, com navegação e logout. */
export function AppLayout({ children }: { children: ReactNode }) {
  const { signOut } = useSession();
  const navigate = useNavigate();

  async function handleSignOut(): Promise<void> {
    await signOut();
    navigate('/entrar');
  }

  return (
    <div className="app-shell">
      <nav className="app-nav" aria-label="Navegação principal">
        <Link to="/painel">Agenda</Link>
        <Link to="/painel/servicos">Serviços</Link>
        <Link to="/painel/equipe">Equipe</Link>
        <Link to="/painel/clientes">Clientes</Link>
        <Link to="/painel/financeiro">Financeiro</Link>
        <Link to="/painel/site">Site</Link>
        <Link to="/painel/assinatura">Assinatura</Link>
        <button type="button" onClick={() => void handleSignOut()}>
          Sair
        </button>
      </nav>
      <main className="app-main">{children}</main>
    </div>
  );
}

// ---------- F14.2: login ----------

export function SignInPage() {
  const { signIn, isLoading, error } = useSession();
  const navigate = useNavigate();

  const [tenantId, setTenantId] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  async function handleSubmit(event: React.FormEvent): Promise<void> {
    event.preventDefault();

    const success = await signIn(tenantId, email, password);

    if (success) {
      navigate('/painel');
    }
  }

  return (
    <main className="auth-shell">
      <form onSubmit={(event) => void handleSubmit(event)} className="auth-form">
        <h1>Entrar no Servia</h1>

        <label htmlFor="tenant">Estabelecimento</label>
        <input
          id="tenant"
          value={tenantId}
          onChange={(event) => setTenantId(event.target.value)}
          required
          autoComplete="organization"
        />

        <label htmlFor="email">E-mail</label>
        <input
          id="email"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          required
          autoComplete="email"
        />

        <label htmlFor="password">Senha</label>
        <input
          id="password"
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
          minLength={8}
          autoComplete="current-password"
        />

        {error !== null && <ErrorMessage message={error} />}

        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </main>
  );
}

// ---------- F14.3: agenda ----------

export function AgendaPage({ client }: { client: ApiClient }) {
  const [appointments, setAppointments] = useState<readonly Appointment[]>([]);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      setLoading(true);
      setError(null);

      try {
        const result = await client.request<readonly Appointment[]>(
          'GET',
          `/api/v1/agenda/appointments?date=${date}`,
        );

        if (!cancelled) {
          setAppointments(result);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : 'Falha ao carregar a agenda.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client, date]);

  return (
    <>
      <PageHeader title="Agenda" subtitle="Atendimentos do dia" />

      <label htmlFor="date">Data</label>
      <input
        id="date"
        type="date"
        value={date}
        onChange={(event) => setDate(event.target.value)}
      />

      {loading && <Loading />}
      {error !== null && <ErrorMessage message={error} />}

      {!loading && error === null && appointments.length === 0 && (
        <EmptyState message="Nenhum atendimento nesta data." />
      )}

      {appointments.length > 0 && (
        <table>
          <caption>Atendimentos de {date}</caption>
          <thead>
            <tr>
              <th scope="col">Horário</th>
              <th scope="col">Duração</th>
              <th scope="col">Status</th>
            </tr>
          </thead>
          <tbody>
            {appointments.map((appointment) => (
              <tr key={appointment.id}>
                <td>{appointment.time}</td>
                <td>{appointment.durationMinutes} min</td>
                <td>{appointment.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

// ---------- F14.3: serviços ----------

export function ServicesPage({ client }: { client: ApiClient }) {
  const [services, setServices] = useState<readonly Service[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      try {
        const page = await client.request<PagedResponse<Service>>(
          'GET',
          '/api/v1/catalog/services?page=1&pageSize=50',
        );

        if (!cancelled) {
          setServices(page.items);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : 'Falha ao carregar serviços.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client]);

  return (
    <>
      <PageHeader title="Serviços" subtitle="Catálogo do estabelecimento" />

      {loading && <Loading />}
      {error !== null && <ErrorMessage message={error} />}
      {!loading && error === null && services.length === 0 && (
        <EmptyState message="Nenhum serviço cadastrado." />
      )}

      {services.length > 0 && (
        <table>
          <caption>Serviços</caption>
          <thead>
            <tr>
              <th scope="col">Nome</th>
              <th scope="col">Preço</th>
              <th scope="col">Duração</th>
              <th scope="col">Ativo</th>
            </tr>
          </thead>
          <tbody>
            {services.map((service) => (
              <tr key={service.id}>
                <td>{service.name}</td>
                <td>R$ {service.price.toFixed(2)}</td>
                <td>{service.durationMinutes} min</td>
                <td>{service.isActive ? 'Sim' : 'Não'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

// ---------- F14.3: equipe ----------

export function TeamPage({ client }: { client: ApiClient }) {
  const [professionals, setProfessionals] = useState<readonly Professional[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      try {
        const page = await client.request<PagedResponse<Professional>>(
          'GET',
          '/api/v1/team/professionals?page=1&pageSize=50',
        );

        if (!cancelled) {
          setProfessionals(page.items);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : 'Falha ao carregar a equipe.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client]);

  return (
    <>
      <PageHeader title="Equipe" subtitle="Profissionais do estabelecimento" />

      {loading && <Loading />}
      {error !== null && <ErrorMessage message={error} />}
      {!loading && error === null && professionals.length === 0 && (
        <EmptyState message="Nenhum profissional cadastrado." />
      )}

      {professionals.length > 0 && (
        <ul>
          {professionals.map((professional) => (
            <li key={professional.id}>
              {professional.name}
              {professional.title !== null && ` — ${professional.title}`}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

// ---------- F14.3: clientes ----------

export function CustomersPage({ client }: { client: ApiClient }) {
  const [customers, setCustomers] = useState<readonly Customer[]>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      setLoading(true);

      try {
        const query = search.length > 0 ? `&search=${encodeURIComponent(search)}` : '';
        const page = await client.request<PagedResponse<Customer>>(
          'GET',
          `/api/v1/customers?page=1&pageSize=50${query}`,
        );

        if (!cancelled) {
          setCustomers(page.items);
          setError(null);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : 'Falha ao carregar clientes.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client, search]);

  return (
    <>
      <PageHeader title="Clientes" subtitle="Cadastro do estabelecimento" />

      <label htmlFor="search">Buscar por nome ou telefone</label>
      <input
        id="search"
        type="search"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />

      {loading && <Loading />}
      {error !== null && <ErrorMessage message={error} />}
      {!loading && error === null && customers.length === 0 && (
        <EmptyState message="Nenhum cliente encontrado." />
      )}

      {customers.length > 0 && (
        <table>
          <caption>Clientes</caption>
          <thead>
            <tr>
              <th scope="col">Nome</th>
              <th scope="col">Telefone</th>
              <th scope="col">E-mail</th>
            </tr>
          </thead>
          <tbody>
            {customers.map((customer) => (
              <tr key={customer.id}>
                <td>{customer.name}</td>
                <td>{customer.phone}</td>
                <td>{customer.email ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

// ---------- F14.4: financeiro ----------

export function FinancePage({ client }: { client: ApiClient }) {
  const [report, setReport] = useState<FinancialReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      const today = new Date();
      const from = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10);
      const to = today.toISOString().slice(0, 10);

      try {
        const result = await client.request<FinancialReport>(
          'GET',
          `/api/v1/finance/report?from=${from}&to=${to}`,
        );

        if (!cancelled) {
          setReport(result);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : 'Falha ao carregar o relatório.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client]);

  return (
    <>
      <PageHeader title="Financeiro" subtitle="Resultado do mês corrente" />

      {loading && <Loading />}
      {error !== null && <ErrorMessage message={error} />}

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

// ---------- F14.5: site ----------

export function SitePage({ client }: { client: ApiClient }) {
  const [site, setSite] = useState<Site | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      try {
        const result = await client.request<Site>('GET', '/api/v1/site');

        if (!cancelled) {
          setSite(result);
        }
      } catch {
        // Site ainda nao criado: nao e erro, e estado inicial.
        if (!cancelled) {
          setSite(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client]);

  return (
    <>
      <PageHeader title="Site" subtitle="Presença online do estabelecimento" />

      {loading && <Loading />}

      {!loading && site === null && (
        <EmptyState message="O site ainda não foi criado." />
      )}

      {site !== null && (
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
      )}
    </>
  );
}

// ---------- F14.8: assinatura ----------

export function SubscriptionPage({ client }: { client: ApiClient }) {
  const [subscription, setSubscription] = useState<Subscription | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      try {
        const result = await client.request<Subscription>('GET', '/api/v1/billing/subscription');

        if (!cancelled) {
          setSubscription(result);
        }
      } catch (exception) {
        if (!cancelled) {
          setError(exception instanceof Error ? exception.message : 'Falha ao carregar a assinatura.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [client]);

  return (
    <>
      <PageHeader title="Assinatura" subtitle="Plano contratado" />

      {loading && <Loading />}
      {error !== null && <ErrorMessage message={error} />}

      {subscription !== null && (
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
    </>
  );
}
