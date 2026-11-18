/**
 * Agendamento público (F14.7).
 *
 * O visitante não está autenticado. Todo o fluxo usa os endpoints públicos
 * `/api/v1/public/site`, que resolvem o estabelecimento pelo host. Nenhum
 * endpoint administrativo é chamado.
 *
 * <b>A disponibilidade é decidida pelo backend.</b> O frontend não recalcula
 * jornada, bloqueio ou conflito: ele apenas apresenta o que o servidor
 * devolve. Duplicar a regra aqui faria a tela oferecer horário que a gravação
 * rejeita.
 *
 * Sequência aprovada: serviço → profissional → data → horário → dados →
 * confirmação. A seleção do profissional depende do serviço escolhido.
 */

import { useCallback, useEffect, useState } from 'react';
import type {
  PublicAppointment,
  PublicAvailability,
  PublicProfessional,
  PublicService,
} from '../api/types';

interface BookingWidgetPageProps {
  readonly baseUrl: string;
}

/** Etapas do fluxo. A tela mostra apenas a etapa corrente. */
type Step = 'service' | 'professional' | 'slot' | 'details' | 'confirmed';

/** Erro de comunicação ou de validação, com a mensagem do servidor. */
interface BookingError {
  readonly message: string;
  /** Conflito de horário (409): o horário foi ocupado entre a consulta e a confirmação. */
  readonly isConflict: boolean;
}

export function BookingWidgetPage({ baseUrl }: BookingWidgetPageProps) {
  const [step, setStep] = useState<Step>('service');

  const [services, setServices] = useState<readonly PublicService[]>([]);
  const [servicesLoading, setServicesLoading] = useState(true);
  const [servicesError, setServicesError] = useState<string | null>(null);

  const [serviceId, setServiceId] = useState('');
  const [professionals, setProfessionals] = useState<readonly PublicProfessional[]>([]);
  const [professionalsLoading, setProfessionalsLoading] = useState(false);
  const [professionalId, setProfessionalId] = useState('');

  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [slots, setSlots] = useState<readonly string[]>([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsLoaded, setSlotsLoaded] = useState(false);
  const [slotsError, setSlotsError] = useState<string | null>(null);
  const [time, setTime] = useState('');

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [bookingError, setBookingError] = useState<BookingError | null>(null);
  const [confirmed, setConfirmed] = useState<PublicAppointment | null>(null);

  const selectedService = services.find((service) => service.id === serviceId) ?? null;
  const selectedProfessional =
    professionals.find((professional) => professional.id === professionalId) ?? null;

  // ---------- Serviços ----------

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      setServicesLoading(true);
      setServicesError(null);

      try {
        const response = await fetch(`${baseUrl}/api/v1/public/site/services`);

        if (!response.ok) {
          if (!cancelled) {
            setServicesError('Não foi possível carregar os serviços.');
          }

          return;
        }

        const body = (await response.json()) as readonly PublicService[];

        if (!cancelled) {
          setServices(body);
        }
      } catch {
        if (!cancelled) {
          setServicesError('Falha de conexão ao carregar os serviços.');
        }
      } finally {
        if (!cancelled) {
          setServicesLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [baseUrl]);

  // ---------- Profissionais do serviço escolhido ----------

  const loadProfessionals = useCallback(
    async (chosenServiceId: string): Promise<void> => {
      setProfessionalsLoading(true);
      setProfessionals([]);
      setProfessionalId('');

      try {
        // O filtro por serviço é do backend: só vêm profissionais habilitados.
        const response = await fetch(
          `${baseUrl}/api/v1/public/site/professionals?serviceId=${chosenServiceId}`,
        );

        if (!response.ok) {
          return;
        }

        setProfessionals((await response.json()) as readonly PublicProfessional[]);
      } catch {
        // Sem profissionais, a etapa fica vazia; a tela informa.
      } finally {
        setProfessionalsLoading(false);
      }
    },
    [baseUrl],
  );

  // ---------- Disponibilidade ----------

  const loadSlots = useCallback(
    async (chosenServiceId: string, chosenProfessionalId: string, chosenDate: string) => {
      setSlotsLoading(true);
      setSlots([]);
      setSlotsLoaded(false);
      setSlotsError(null);
      setTime('');

      try {
        const query = new URLSearchParams({
          serviceId: chosenServiceId,
          professionalId: chosenProfessionalId,
          date: chosenDate,
        });

        const response = await fetch(
          `${baseUrl}/api/v1/public/site/availability?${query.toString()}`,
        );

        if (response.status === 400) {
          // Pedido inválido não é "sem horários".
          setSlotsError('Escolha serviço, profissional e data para ver os horários.');

          return;
        }

        if (response.status === 404) {
          setSlotsError('Serviço ou profissional indisponível neste estabelecimento.');

          return;
        }

        if (!response.ok) {
          setSlotsError('Não foi possível consultar os horários.');

          return;
        }

        const body = (await response.json()) as PublicAvailability;

        setSlots(body.slots);
        setSlotsLoaded(true);
      } catch {
        setSlotsError('Falha de conexão ao consultar os horários.');
      } finally {
        setSlotsLoading(false);
      }
    },
    [baseUrl],
  );

  // ---------- Ações ----------

  function chooseService(id: string): void {
    setServiceId(id);
    setProfessionalId('');
    setSlots([]);
    setSlotsLoaded(false);
    setTime('');
    setBookingError(null);
    setStep('professional');

    void loadProfessionals(id);
  }

  function chooseProfessional(id: string): void {
    setProfessionalId(id);
    setSlots([]);
    setSlotsLoaded(false);
    setTime('');
    setBookingError(null);
    setStep('slot');

    void loadSlots(serviceId, id, date);
  }

  function changeDate(nextDate: string): void {
    setDate(nextDate);
    setTime('');

    // Só consulta com os três parâmetros completos: evita requisição
    // desatualizada quando o usuário ainda está escolhendo.
    if (serviceId !== '' && professionalId !== '' && nextDate !== '') {
      void loadSlots(serviceId, professionalId, nextDate);
    }
  }

  function chooseSlot(slot: string): void {
    setTime(slot);
    setBookingError(null);
    setStep('details');
  }

  function restart(): void {
    setStep('service');
    setServiceId('');
    setProfessionalId('');
    setProfessionals([]);
    setSlots([]);
    setSlotsLoaded(false);
    setTime('');
    setName('');
    setPhone('');
    setEmail('');
    setBookingError(null);
    setConfirmed(null);
  }

  async function confirmBooking(event: React.FormEvent): Promise<void> {
    event.preventDefault();
    setSubmitting(true);
    setBookingError(null);

    try {
      const response = await fetch(`${baseUrl}/api/v1/public/site/appointments`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name,
          phone,
          email: email === '' ? null : email,
          serviceId,
          professionalId,
          date,
          time,
        }),
      });

      if (response.status === 409) {
        // O horário foi ocupado entre a consulta e a confirmação.
        setBookingError({
          message: 'Este horário acabou de ser ocupado. Escolha outro.',
          isConflict: true,
        });

        return;
      }

      if (response.status === 400) {
        setBookingError({
          message: 'Confira os dados informados e tente novamente.',
          isConflict: false,
        });

        return;
      }

      if (!response.ok) {
        setBookingError({
          message: 'Não foi possível concluir o agendamento.',
          isConflict: false,
        });

        return;
      }

      setConfirmed((await response.json()) as PublicAppointment);
      setStep('confirmed');
    } catch {
      setBookingError({
        message: 'Falha de conexão ao confirmar o agendamento.',
        isConflict: false,
      });
    } finally {
      setSubmitting(false);
    }
  }

  // ---------- Renderização ----------

  return (
    <main className="booking-widget">
      <h1>Agendar horário</h1>

      {step === 'service' && (
        <section aria-labelledby="service-heading">
          <h2 id="service-heading">1. Escolha o serviço</h2>

          {servicesLoading && <p role="status">Carregando serviços…</p>}

          {servicesError !== null && (
            <p role="alert" className="error">
              {servicesError}
            </p>
          )}

          {!servicesLoading && servicesError === null && services.length === 0 && (
            <p className="muted">Nenhum serviço disponível para agendamento.</p>
          )}

          {services.length > 0 && (
            <ul className="service-list">
              {services.map((service) => (
                <li key={service.id}>
                  <button type="button" onClick={() => chooseService(service.id)}>
                    <strong>{service.name}</strong>
                    <span>
                      R$ {service.price.toFixed(2)} · {service.durationMinutes} min
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {step === 'professional' && selectedService !== null && (
        <section aria-labelledby="professional-heading">
          <h2 id="professional-heading">2. Escolha o profissional</h2>
          <p className="muted">Serviço: {selectedService.name}</p>

          {professionalsLoading && <p role="status">Carregando profissionais…</p>}

          {!professionalsLoading && professionals.length === 0 && (
            <p className="muted">
              Nenhum profissional disponível para este serviço no momento.
            </p>
          )}

          {professionals.length > 0 && (
            <ul className="professional-list">
              {professionals.map((professional) => (
                <li key={professional.id}>
                  <button type="button" onClick={() => chooseProfessional(professional.id)}>
                    {professional.name}
                  </button>
                </li>
              ))}
            </ul>
          )}

          <button type="button" className="link" onClick={() => setStep('service')}>
            Voltar
          </button>
        </section>
      )}

      {step === 'slot' && selectedProfessional !== null && (
        <section aria-labelledby="slot-heading">
          <h2 id="slot-heading">3. Escolha a data e o horário</h2>
          <p className="muted">Profissional: {selectedProfessional.name}</p>

          <label htmlFor="booking-date">Data</label>
          <input
            id="booking-date"
            type="date"
            value={date}
            onChange={(event) => changeDate(event.target.value)}
            required
          />

          {slotsLoading && <p role="status">Consultando horários…</p>}

          {slotsError !== null && (
            <p role="alert" className="error">
              {slotsError}
            </p>
          )}

          {!slotsLoading && slotsError === null && slotsLoaded && slots.length === 0 && (
            <p className="muted">Nenhum horário disponível nesta data.</p>
          )}

          {slots.length > 0 && (
            <ul className="slot-list">
              {slots.map((slot) => (
                <li key={slot}>
                  <button type="button" onClick={() => chooseSlot(slot)}>
                    {slot}
                  </button>
                </li>
              ))}
            </ul>
          )}

          <button type="button" className="link" onClick={() => setStep('professional')}>
            Voltar
          </button>
        </section>
      )}

      {step === 'details' && (
        <section aria-labelledby="details-heading">
          <h2 id="details-heading">4. Seus dados</h2>

          <p className="muted">
            {selectedService?.name} · {selectedProfessional?.name} · {date} às {time}
          </p>

          <form onSubmit={(event) => void confirmBooking(event)}>
            <label htmlFor="booking-name">Nome</label>
            <input
              id="booking-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={150}
              required
            />

            <label htmlFor="booking-phone">Telefone</label>
            <input
              id="booking-phone"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              maxLength={30}
              required
            />

            <label htmlFor="booking-email">E-mail (opcional)</label>
            <input
              id="booking-email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              maxLength={200}
            />

            {bookingError !== null && (
              <p role="alert" className="error">
                {bookingError.message}
              </p>
            )}

            <button type="submit" disabled={submitting}>
              {submitting ? 'Confirmando…' : 'Confirmar agendamento'}
            </button>
          </form>

          <button
            type="button"
            className="link"
            onClick={() => {
              // Conflito exige novo horário; demais erros voltam aos dados.
              if (bookingError?.isConflict === true) {
                setBookingError(null);
                setStep('slot');
                void loadSlots(serviceId, professionalId, date);

                return;
              }

              setStep('slot');
            }}
          >
            Voltar
          </button>
        </section>
      )}

      {step === 'confirmed' && confirmed !== null && (
        <section aria-labelledby="confirmed-heading">
          <h2 id="confirmed-heading">Agendamento confirmado</h2>

          <p role="status">
            {selectedService?.name} com {selectedProfessional?.name} em {confirmed.date} às{' '}
            {confirmed.time}.
          </p>

          <button type="button" onClick={restart}>
            Agendar outro horário
          </button>
        </section>
      )}
    </main>
  );
}
