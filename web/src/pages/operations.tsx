/**
 * Páginas de clientes e agenda com escrita (F18.4, F18.1).
 */

import { useCallback, useEffect, useState } from 'react';
import type { ApiClient, PagedResponse } from '../api/client';
import type { Appointment, Customer, Professional, Service } from '../api/types';
import {
  Field,
  FormError,
  FormPanel,
  FormSuccess,
  SubmitButton,
  useSubmit,
} from '../components/forms';
import { EmptyState, ErrorMessage, Loading, PageHeader } from './index';

// ---------- F18.4: clientes ----------

export function CustomersPage({ client }: { client: ApiClient }) {
  const [customers, setCustomers] = useState<readonly Customer[]>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isFormOpen, setFormOpen] = useState(false);

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const query = search.length > 0 ? `&search=${encodeURIComponent(search)}` : '';
      const page = await client.request<PagedResponse<Customer>>(
        'GET',
        `/api/v1/customers?page=1&pageSize=50${query}`,
      );

      setCustomers(page.items);
      setLoadError(null);
    } catch (exception) {
      setLoadError(exception instanceof Error ? exception.message : 'Falha ao carregar clientes.');
    } finally {
      setLoading(false);
    }
  }, [client, search]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleCreate(event: React.FormEvent): Promise<void> {
    event.preventDefault();

    const created = await form.submit(
      async () => {
        await client.request<Customer>('POST', '/api/v1/customers', {
          name,
          phone,
          email: email.length > 0 ? email : null,
        });
      },
      'Cliente cadastrado.',
    );

    if (created) {
      setName('');
      setPhone('');
      setEmail('');
      setFormOpen(false);
      await load();
    }
  }

  async function handleAnonymize(id: string): Promise<void> {
    const done = await form.submit(
      async () => {
        await client.request<void>('POST', `/api/v1/customers/${id}/anonymize`);
      },
      'Dados pessoais anonimizados.',
    );

    if (done) {
      await load();
    }
  }

  return (
    <>
      <PageHeader title="Clientes" subtitle="Cadastro do estabelecimento" />

      <FormPanel
        title="Novo cliente"
        isOpen={isFormOpen}
        onToggle={() => setFormOpen(!isFormOpen)}
      >
        <form onSubmit={(event) => void handleCreate(event)}>
          <Field id="cust-name" label="Nome" value={name} onChange={setName} required />
          <Field id="cust-phone" label="Telefone" value={phone} onChange={setPhone} required />
          <Field id="cust-email" label="E-mail" type="email" value={email} onChange={setEmail} />

          <FormError message={form.error} />
          <SubmitButton isSubmitting={form.isSubmitting} label="Cadastrar cliente" />
        </form>
      </FormPanel>

      <FormSuccess message={form.success} />

      <label htmlFor="search">Buscar por nome ou telefone</label>
      <input
        id="search"
        type="search"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />

      {loading && <Loading />}
      {loadError !== null && <ErrorMessage message={loadError} />}

      {!loading && loadError === null && customers.length === 0 && (
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
              <th scope="col">Ações</th>
            </tr>
          </thead>
          <tbody>
            {customers.map((customer) => (
              <tr key={customer.id}>
                <td>{customer.name}</td>
                <td>{customer.phone}</td>
                <td>{customer.email ?? '—'}</td>
                <td>
                  <button
                    type="button"
                    className="btn-tertiary"
                    onClick={() => void handleAnonymize(customer.id)}
                    disabled={form.isSubmitting}
                  >
                    Anonimizar
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

// ---------- F18.1: agenda ----------

export function AgendaPage({ client }: { client: ApiClient }) {
  const [appointments, setAppointments] = useState<readonly Appointment[]>([]);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isFormOpen, setFormOpen] = useState(false);

  const [customers, setCustomers] = useState<readonly Customer[]>([]);
  const [services, setServices] = useState<readonly Service[]>([]);
  const [professionals, setProfessionals] = useState<readonly Professional[]>([]);

  const [customerId, setCustomerId] = useState('');
  const [serviceId, setServiceId] = useState('');
  const [professionalId, setProfessionalId] = useState('');
  const [time, setTime] = useState('09:00');
  const [duration, setDuration] = useState('30');

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const result = await client.request<readonly Appointment[]>(
        'GET',
        `/api/v1/agenda/appointments?date=${date}`,
      );

      setAppointments(result);
      setLoadError(null);
    } catch (exception) {
      setLoadError(exception instanceof Error ? exception.message : 'Falha ao carregar a agenda.');
    } finally {
      setLoading(false);
    }
  }, [client, date]);

  useEffect(() => {
    void load();
  }, [load]);

  // Referencias para o formulario: carregadas uma vez.
  useEffect(() => {
    let cancelled = false;

    async function loadReferences(): Promise<void> {
      try {
        const [customerPage, servicePage, professionalPage] = await Promise.all([
          client.request<PagedResponse<Customer>>('GET', '/api/v1/customers?page=1&pageSize=100'),
          client.request<PagedResponse<Service>>('GET', '/api/v1/catalog/services?page=1&pageSize=100'),
          client.request<PagedResponse<Professional>>('GET', '/api/v1/team/professionals?page=1&pageSize=100'),
        ]);

        if (!cancelled) {
          setCustomers(customerPage.items);
          setServices(servicePage.items);
          setProfessionals(professionalPage.items);
        }
      } catch {
        // Sem referencias, o formulario fica indisponivel; a lista continua.
      }
    }

    void loadReferences();

    return () => {
      cancelled = true;
    };
  }, [client]);

  async function handleCreate(event: React.FormEvent): Promise<void> {
    event.preventDefault();

    const created = await form.submit(
      async () => {
        await client.request<Appointment>('POST', '/api/v1/agenda/appointments', {
          customerId,
          serviceId,
          professionalId,
          date,
          time: `${time}:00`,
          durationMinutes: Number(duration),
        });
      },
      'Agendamento criado.',
    );

    if (created) {
      setFormOpen(false);
      await load();
    }
  }

  async function handleCancel(id: string): Promise<void> {
    const done = await form.submit(
      async () => {
        await client.request<Appointment>('POST', `/api/v1/agenda/appointments/${id}/cancel`, {
          reason: null,
        });
      },
      'Agendamento cancelado.',
    );

    if (done) {
      await load();
    }
  }

  async function handleComplete(id: string): Promise<void> {
    const done = await form.submit(
      async () => {
        await client.request<Appointment>('POST', `/api/v1/agenda/appointments/${id}/complete`);
      },
      'Atendimento concluído.',
    );

    if (done) {
      await load();
    }
  }

  const canCreate =
    customers.length > 0 && services.length > 0 && professionals.length > 0;

  return (
    <>
      <PageHeader title="Agenda" subtitle="Atendimentos do dia" />

      {canCreate && (
        <FormPanel
          title="Novo agendamento"
          isOpen={isFormOpen}
          onToggle={() => setFormOpen(!isFormOpen)}
        >
          <form onSubmit={(event) => void handleCreate(event)}>
            <div className="field">
              <label htmlFor="appt-customer">Cliente</label>
              <select
                id="appt-customer"
                value={customerId}
                onChange={(event) => setCustomerId(event.target.value)}
                required
              >
                <option value="">Selecione</option>
                {customers.map((customer) => (
                  <option key={customer.id} value={customer.id}>
                    {customer.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="field">
              <label htmlFor="appt-service">Serviço</label>
              <select
                id="appt-service"
                value={serviceId}
                onChange={(event) => setServiceId(event.target.value)}
                required
              >
                <option value="">Selecione</option>
                {services.map((service) => (
                  <option key={service.id} value={service.id}>
                    {service.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="field">
              <label htmlFor="appt-professional">Profissional</label>
              <select
                id="appt-professional"
                value={professionalId}
                onChange={(event) => setProfessionalId(event.target.value)}
                required
              >
                <option value="">Selecione</option>
                {professionals.map((professional) => (
                  <option key={professional.id} value={professional.id}>
                    {professional.name}
                  </option>
                ))}
              </select>
            </div>

            <Field id="appt-time" label="Horário" type="time" value={time} onChange={setTime} required />
            <Field
              id="appt-duration"
              label="Duração (minutos)"
              type="number"
              value={duration}
              onChange={setDuration}
              required
              min={1}
              max={1440}
            />

            <FormError message={form.error} />
            <SubmitButton isSubmitting={form.isSubmitting} label="Agendar" />
          </form>
        </FormPanel>
      )}

      <FormSuccess message={form.success} />

      <label htmlFor="date">Data</label>
      <input
        id="date"
        type="date"
        value={date}
        onChange={(event) => setDate(event.target.value)}
      />

      {loading && <Loading />}
      {loadError !== null && <ErrorMessage message={loadError} />}

      {!loading && loadError === null && appointments.length === 0 && (
        <EmptyState message="Nenhum atendimento nesta data." />
      )}

      {appointments.length > 0 && (
        <table>
          <caption>Atendimentos de {date}</caption>
          <thead>
            <tr>
              <th scope="col">Horário</th>
              <th scope="col">Duração</th>
              <th scope="col">Situação</th>
              <th scope="col">Ações</th>
            </tr>
          </thead>
          <tbody>
            {appointments.map((appointment) => (
              <tr key={appointment.id}>
                <td>{appointment.time}</td>
                <td>{appointment.durationMinutes} min</td>
                <td>{appointment.status}</td>
                <td>
                  {appointment.status === 'Confirmed' && (
                    <>
                      <button
                        type="button"
                        className="btn-tertiary"
                        onClick={() => void handleComplete(appointment.id)}
                        disabled={form.isSubmitting}
                      >
                        Concluir
                      </button>
                      <button
                        type="button"
                        className="btn-tertiary"
                        onClick={() => void handleCancel(appointment.id)}
                        disabled={form.isSubmitting}
                      >
                        Cancelar
                      </button>
                    </>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
