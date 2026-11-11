/**
 * Páginas de catálogo e equipe com escrita (F18.2, F18.3).
 *
 * Antes, estas páginas apenas listavam. Agora criam, alteram e desativam
 * pela interface — a API já suportava, faltava a tela.
 */

import { useCallback, useEffect, useState } from 'react';
import type { ApiClient, PagedResponse } from '../api/client';
import type { Professional, Service } from '../api/types';
import {
  Field,
  FormError,
  FormPanel,
  FormSuccess,
  SubmitButton,
  useSubmit,
} from '../components/forms';
import { EmptyState, ErrorMessage, Loading, PageHeader } from './index';

// ---------- F18.2: serviços ----------

export function ServicesPage({ client }: { client: ApiClient }) {
  const [services, setServices] = useState<readonly Service[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isFormOpen, setFormOpen] = useState(false);

  const [name, setName] = useState('');
  const [price, setPrice] = useState('');
  const [duration, setDuration] = useState('30');

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const page = await client.request<PagedResponse<Service>>(
        'GET',
        '/api/v1/catalog/services?page=1&pageSize=50',
      );

      setServices(page.items);
      setLoadError(null);
    } catch (exception) {
      setLoadError(exception instanceof Error ? exception.message : 'Falha ao carregar serviços.');
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
        await client.request<Service>('POST', '/api/v1/catalog/services', {
          name,
          price: Number(price),
          durationMinutes: Number(duration),
        });
      },
      'Serviço criado.',
    );

    if (created) {
      setName('');
      setPrice('');
      setDuration('30');
      setFormOpen(false);
      await load();
    }
  }

  async function handleDeactivate(id: string): Promise<void> {
    const done = await form.submit(
      async () => {
        await client.request<void>('POST', `/api/v1/catalog/services/${id}/deactivate`);
      },
      'Serviço desativado.',
    );

    if (done) {
      await load();
    }
  }

  return (
    <>
      <PageHeader title="Serviços" subtitle="Catálogo do estabelecimento" />

      <FormPanel
        title="Novo serviço"
        isOpen={isFormOpen}
        onToggle={() => setFormOpen(!isFormOpen)}
      >
        <form onSubmit={(event) => void handleCreate(event)}>
          <Field id="service-name" label="Nome" value={name} onChange={setName} required />
          <Field
            id="service-price"
            label="Preço (R$)"
            type="number"
            value={price}
            onChange={setPrice}
            required
            min={0.01}
            step="0.01"
          />
          <Field
            id="service-duration"
            label="Duração (minutos)"
            type="number"
            value={duration}
            onChange={setDuration}
            required
            min={1}
            max={1440}
          />

          <FormError message={form.error} />
          <SubmitButton isSubmitting={form.isSubmitting} label="Criar serviço" />
        </form>
      </FormPanel>

      <FormSuccess message={form.success} />

      {loading && <Loading />}
      {loadError !== null && <ErrorMessage message={loadError} />}

      {!loading && loadError === null && services.length === 0 && (
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
              <th scope="col">Situação</th>
              <th scope="col">Ações</th>
            </tr>
          </thead>
          <tbody>
            {services.map((service) => (
              <tr key={service.id}>
                <td>{service.name}</td>
                <td>R$ {service.price.toFixed(2)}</td>
                <td>{service.durationMinutes} min</td>
                <td>{service.isActive ? 'Ativo' : 'Inativo'}</td>
                <td>
                  {service.isActive && (
                    <button
                      type="button"
                      className="btn-tertiary"
                      onClick={() => void handleDeactivate(service.id)}
                      disabled={form.isSubmitting}
                    >
                      Desativar
                    </button>
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

// ---------- F18.3: equipe ----------

const WEEK_DAYS = [
  { value: 1, label: 'Seg' },
  { value: 2, label: 'Ter' },
  { value: 3, label: 'Qua' },
  { value: 4, label: 'Qui' },
  { value: 5, label: 'Sex' },
  { value: 6, label: 'Sáb' },
  { value: 0, label: 'Dom' },
] as const;

export function TeamPage({ client }: { client: ApiClient }) {
  const [professionals, setProfessionals] = useState<readonly Professional[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isFormOpen, setFormOpen] = useState(false);

  const [name, setName] = useState('');
  const [title, setTitle] = useState('');
  const [startTime, setStartTime] = useState('09:00');
  const [endTime, setEndTime] = useState('18:00');
  const [workDays, setWorkDays] = useState<readonly number[]>([1, 2, 3, 4, 5]);

  const form = useSubmit();

  const load = useCallback(async () => {
    setLoading(true);

    try {
      const page = await client.request<PagedResponse<Professional>>(
        'GET',
        '/api/v1/team/professionals?page=1&pageSize=50',
      );

      setProfessionals(page.items);
      setLoadError(null);
    } catch (exception) {
      setLoadError(exception instanceof Error ? exception.message : 'Falha ao carregar a equipe.');
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    void load();
  }, [load]);

  function toggleDay(day: number): void {
    setWorkDays((current) =>
      current.includes(day) ? current.filter((item) => item !== day) : [...current, day],
    );
  }

  async function handleCreate(event: React.FormEvent): Promise<void> {
    event.preventDefault();

    const created = await form.submit(
      async () => {
        await client.request<Professional>('POST', '/api/v1/team/professionals', {
          name,
          title: title.length > 0 ? title : null,
          workDays,
          startTime: `${startTime}:00`,
          endTime: `${endTime}:00`,
          slotIntervalMinutes: 30,
        });
      },
      'Profissional criado.',
    );

    if (created) {
      setName('');
      setTitle('');
      setFormOpen(false);
      await load();
    }
  }

  return (
    <>
      <PageHeader title="Equipe" subtitle="Profissionais do estabelecimento" />

      <FormPanel
        title="Novo profissional"
        isOpen={isFormOpen}
        onToggle={() => setFormOpen(!isFormOpen)}
      >
        <form onSubmit={(event) => void handleCreate(event)}>
          <Field id="prof-name" label="Nome" value={name} onChange={setName} required />
          <Field
            id="prof-title"
            label="Cargo"
            value={title}
            onChange={setTitle}
            placeholder="Barbeiro, cabeleireira…"
          />

          <fieldset className="field">
            <legend>Dias de trabalho</legend>
            {WEEK_DAYS.map((day) => (
              <label key={day.value} className="checkbox-inline">
                <input
                  type="checkbox"
                  checked={workDays.includes(day.value)}
                  onChange={() => toggleDay(day.value)}
                />
                {day.label}
              </label>
            ))}
          </fieldset>

          <Field
            id="prof-start"
            label="Início da jornada"
            type="time"
            value={startTime}
            onChange={setStartTime}
            required
          />
          <Field
            id="prof-end"
            label="Fim da jornada"
            type="time"
            value={endTime}
            onChange={setEndTime}
            required
          />

          <FormError message={form.error} />
          <SubmitButton isSubmitting={form.isSubmitting} label="Criar profissional" />
        </form>
      </FormPanel>

      <FormSuccess message={form.success} />

      {loading && <Loading />}
      {loadError !== null && <ErrorMessage message={loadError} />}

      {!loading && loadError === null && professionals.length === 0 && (
        <EmptyState message="Nenhum profissional cadastrado." />
      )}

      {professionals.length > 0 && (
        <ul className="list">
          {professionals.map((professional) => (
            <li key={professional.id}>
              <strong>{professional.name}</strong>
              {professional.title !== null && <span className="muted"> — {professional.title}</span>}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
