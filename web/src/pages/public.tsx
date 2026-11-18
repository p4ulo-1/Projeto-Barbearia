/**
 * Páginas públicas (F14.6, F14.7).
 *
 * O site do tenant e o widget de agendamento são acessíveis sem login:
 * é o que o cliente final vê.
 */

import { useEffect, useState } from 'react';
import type { Availability, Service } from '../api/types';

interface PublicSitePageProps {
  readonly baseUrl: string;
}

/**
 * Site público do estabelecimento (F14.6).
 *
 * Renderiza o snapshot publicado. O conteúdo vem do servidor, não é
 * hardcoded: cada tenant tem o seu.
 */
export function PublicSitePage({ baseUrl }: PublicSitePageProps) {
  const [services, setServices] = useState<readonly Service[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      try {
        const response = await fetch(`${baseUrl}/api/v1/catalog/services?page=1&pageSize=20`);

        if (response.ok) {
          const page = (await response.json()) as { items: readonly Service[] };

          if (!cancelled) {
            setServices(page.items);
          }
        }
      } catch {
        // Site publico degrada para conteudo vazio, nao para erro.
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
  }, [baseUrl]);

  return (
    <main className="public-site">
      <header className="hero">
        <h1>Bem-vindo</h1>
        <p>Agende seu horário em poucos passos.</p>
        <a href="/agendar" className="cta">
          Agendar agora
        </a>
      </header>

      <section aria-labelledby="services-heading">
        <h2 id="services-heading">Serviços</h2>

        {loading && <p role="status">Carregando…</p>}

        {!loading && services.length === 0 && (
          <p className="muted">Nenhum serviço disponível no momento.</p>
        )}

        {services.length > 0 && (
          <ul className="service-list">
            {services.map((service) => (
              <li key={service.id}>
                <strong>{service.name}</strong>
                <span>R$ {service.price.toFixed(2)}</span>
                <span>{service.durationMinutes} min</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}

interface BookingWidgetPageProps {
  readonly baseUrl: string;
}

/**
 * Widget de agendamento (F14.7).
 *
 * Consulta a disponibilidade real da agenda. O cliente não vê horário que
 * não pode ser reservado.
 */
export function BookingWidgetPage({ baseUrl }: BookingWidgetPageProps) {
  const [services, setServices] = useState<readonly Service[]>([]);
  const [serviceId, setServiceId] = useState('');
  const [professionalId, setProfessionalId] = useState('');
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [availability, setAvailability] = useState<Availability | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // O serviço é obrigatório na consulta: a duração vem do cadastro dele.
  useEffect(() => {
    let cancelled = false;

    async function loadServices(): Promise<void> {
      try {
        const response = await fetch(`${baseUrl}/api/v1/catalog/services?page=1&pageSize=50`);

        if (!response.ok) {
          return;
        }

        const page = (await response.json()) as { items: readonly Service[] };

        if (!cancelled) {
          setServices(page.items);
        }
      } catch {
        // Sem serviços, o widget fica sem opção; a página não quebra.
      }
    }

    void loadServices();

    return () => {
      cancelled = true;
    };
  }, [baseUrl]);

  async function handleSearch(event: React.FormEvent): Promise<void> {
    event.preventDefault();
    setLoading(true);
    setError(null);
    setAvailability(null);

    try {
      const query = new URLSearchParams({ professionalId, serviceId, date });
      const response = await fetch(`${baseUrl}/api/v1/agenda/availability?${query.toString()}`);

      if (!response.ok) {
        // Erro de requisição não é "sem horários": a mensagem é distinta.
        setError('Não foi possível consultar a disponibilidade.');

        return;
      }

      setAvailability((await response.json()) as Availability);
    } catch {
      setError('Falha de conexão.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="booking-widget">
      <h1>Agendar horário</h1>

      <form onSubmit={(event) => void handleSearch(event)}>
        <label htmlFor="service">Serviço</label>
        <select
          id="service"
          value={serviceId}
          onChange={(event) => setServiceId(event.target.value)}
          required
        >
          <option value="">Selecione um serviço</option>
          {services.map((service) => (
            <option key={service.id} value={service.id}>
              {service.name} ({service.durationMinutes} min)
            </option>
          ))}
        </select>

        <label htmlFor="professional">Profissional</label>
        <input
          id="professional"
          value={professionalId}
          onChange={(event) => setProfessionalId(event.target.value)}
          required
        />

        <label htmlFor="booking-date">Data</label>
        <input
          id="booking-date"
          type="date"
          value={date}
          onChange={(event) => setDate(event.target.value)}
          required
        />

        <button type="submit" disabled={loading}>
          {loading ? 'Consultando…' : 'Ver horários'}
        </button>
      </form>

      {error !== null && (
        <p role="alert" className="error">
          {error}
        </p>
      )}

      {availability !== null && (
        <section aria-labelledby="slots-heading">
          <h2 id="slots-heading">Horários disponíveis</h2>

          {availability.availableSlots.length === 0 ? (
            <p className="muted">Nenhum horário disponível nesta data.</p>
          ) : (
            <ul className="slot-list">
              {availability.availableSlots.map((slot) => (
                <li key={slot}>
                  <button type="button">{slot}</button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </main>
  );
}
