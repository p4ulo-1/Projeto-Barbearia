import { useEffect, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { TenantSite } from "./TenantSite";
import type { GeneratedSite } from "@/lib/site-templates";
import { ApiError, IS_DEMO } from "@/lib/api/http";
import {
  publicSite,
  type PublicSite as PublicSiteDto,
  type PublicProfessional,
  type PublicService,
} from "@/lib/api/public-site";
import { Alert, Button, Spinner, TextField } from "@/components/ui-sv";
import { SiteSections } from "./SiteSections";
import s from "./styles";

export function PublicSite({ host, slug = "" }: { host: string; slug?: string }) {
  const key = ["public-site", host];
  const site = useQuery({
    queryKey: key,
    queryFn: (): Promise<PublicSiteDto & { generated?: GeneratedSite }> => publicSite.site(host),
    retry: false,
  });
  const services = useQuery({
    queryKey: [...key, "services"],
    queryFn: () => publicSite.services(host),
    enabled: site.isSuccess,
  });
  const professionals = useQuery({
    queryKey: [...key, "professionals"],
    queryFn: () => publicSite.professionals(host),
    enabled: site.isSuccess,
  });
  const icon = site.data?.generated?.icon;
  useEffect(() => {
    if (!icon) return;
    const original = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    const link = original ?? document.createElement("link");
    const previous = link.getAttribute("href");
    link.rel = "icon";
    link.href = icon;
    if (!original) document.head.appendChild(link);
    return () => {
      if (previous) link.setAttribute("href", previous);
      else link.remove();
    };
  }, [icon]);
  if (site.isPending)
    return (
      <main className={s.publicShell}>
        <Spinner label="Carregando o site…" />
      </main>
    );
  if (site.isError)
    return (
      <main className={s.publicShell}>
        <h1>Site indisponível</h1>
        <Alert tone="error">{site.error.message}</Alert>
        {!(site.error instanceof ApiError && site.error.status === 404) && (
          <Button onClick={() => void site.refetch()}>Tentar novamente</Button>
        )}
      </main>
    );
  const pages = [...site.data.pages].sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
  const page = pages.find((entry) => entry.slug === slug);
  const catalog = services.data;
  const people = professionals.data;
  const renderBooking = (config: Record<string, string>) =>
    catalog && people ? (
      <BookingForm host={host} services={catalog} professionals={people} config={config} />
    ) : services.isError || professionals.isError ? (
      <Alert tone="warning">
        Agendamento temporariamente indisponível. Consulte os <a href="#contato">contatos</a>.
      </Alert>
    ) : (
      <Spinner label="Carregando opções de atendimento…" />
    );
  const content = {
    ...(catalog ? { services: catalog } : {}),
    ...(people ? { professionals: people } : {}),
    booking: renderBooking,
  };
  if (site.data.generated && page) {
    return (
      <main>
        <p className={s.tenantDemoNotice}>Demonstração neste navegador</p>
        <TenantSite site={site.data.generated} {...content} />
      </main>
    );
  }
  return (
    <main className={s.publicShell}>
      <header className={s.publicHeader}>
        <a href="/">{site.data.businessName}</a>
        {pages.length > 1 && (
          <nav aria-label="Páginas do estabelecimento">
            {pages.map((entry) => (
              <a
                key={entry.slug}
                aria-current={entry.slug === slug ? "page" : undefined}
                href={entry.slug ? `/${encodeURIComponent(entry.slug)}` : "/"}
              >
                {entry.title}
              </a>
            ))}
          </nav>
        )}
      </header>
      {!page ? (
        <Alert tone="warning">Página não encontrada.</Alert>
      ) : (
        <>
          {(services.isError || professionals.isError) && (
            <Alert tone="error">
              Não foi possível carregar as opções de atendimento.{" "}
              <Button
                variant="ghost"
                onClick={() => {
                  void services.refetch();
                  void professionals.refetch();
                }}
              >
                Tentar novamente
              </Button>
            </Alert>
          )}
          <SiteSections sections={page.sections} theme={site.data.theme} {...content} />
        </>
      )}
    </main>
  );
}

function bookingError(error: Error) {
  const status = error instanceof ApiError ? error.status : undefined;
  switch (status) {
    case 400:
      return "Confira os dados de contato e a seleção de horário.";
    case 409:
      return "O horário deixou de estar disponível. Escolha outro na lista atualizada.";
    case 429:
      return "Limite de tentativas atingido. Aguarde um momento.";
    case 403:
    case 404:
    case 423:
      return "Agendamento temporariamente indisponível. Consulte os contatos do estabelecimento.";
    default:
      return "Não foi possível conectar ao agendamento. Tente novamente.";
  }
}

function BookingForm({
  host,
  services,
  professionals,
  config,
}: {
  host: string;
  services: PublicService[];
  professionals: PublicProfessional[];
  config: Record<string, string>;
}) {
  const qc = useQueryClient();
  const submitting = useRef(false);
  const [serviceId, setService] = useState(config["serviceId"] ?? "");
  const [professionalId, setProfessional] = useState(config["professionalId"] ?? "");
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  const selectedService = services.find((entry) => entry.id === serviceId);
  const people = professionals.filter((entry) => entry.serviceIds.includes(serviceId));
  const validSelection = !!selectedService && people.some((entry) => entry.id === professionalId);
  const slots = useQuery({
    queryKey: ["public-site", host, "availability", serviceId, professionalId, date],
    queryFn: () => publicSite.availability(host, serviceId, professionalId, date),
    enabled: validSelection && !!date,
  });
  const book = useMutation({
    mutationFn: (data: FormData) => {
      const email = String(data.get("email") ?? "").trim();
      return publicSite.book(host, {
        name: String(data.get("name") ?? "").trim(),
        phone: String(data.get("phone") ?? "").trim(),
        ...(email ? { email } : {}),
        serviceId,
        professionalId,
        date,
        time,
      });
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) setTime("");
    },
    onSettled: () => {
      submitting.current = false;
      return qc.invalidateQueries({ queryKey: ["public-site", host, "availability"] });
    },
  });
  const handle = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!submitting.current && !slots.isFetching && slots.data?.slots.includes(time) && !book.isPending) {
      submitting.current = true;
      book.mutate(new FormData(event.currentTarget));
    }
  };
  if (book.data)
    return (
      <Alert tone="success" title="Agendamento confirmado">
        {new Intl.DateTimeFormat("pt-BR", { dateStyle: "long", timeZone: "UTC" }).format(
          new Date(book.data.date + "T12:00:00Z"),
        )}{" "}
        às {book.data.time.slice(0, 5)}.
      </Alert>
    );
  if (
    !services.some((entry) => professionals.some((person) => person.serviceIds.includes(entry.id)))
  )
    return (
      <p>
        Agendamento online ainda indisponível. Consulte os <a href="#contato">contatos</a> do
        estabelecimento.
      </p>
    );

  return (
    <form className={s.publicForm} onSubmit={handle}>
      <fieldset disabled={book.isPending} className={s.bookingFields}>
        <label>
          Serviço
          <select
            value={serviceId}
            onChange={(event) => {
              setService(event.target.value);
              setProfessional("");
              setTime("");
            }}
            required
          >
            <option value="">Selecione um serviço</option>
            {services.map((entry) => (
              <option key={entry.id} value={entry.id}>
                {entry.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Profissional
          <select
            value={professionalId}
            onChange={(event) => {
              setProfessional(event.target.value);
              setTime("");
            }}
            disabled={!people.length}
            required
          >
            <option value="">Selecione um profissional</option>
            {people.map((entry) => (
              <option key={entry.id} value={entry.id}>
                {entry.name}
              </option>
            ))}
          </select>
        </label>
        {selectedService && (
          <p className={s.bookingSummary}>
            {new Intl.NumberFormat("pt-BR", {
              style: "currency",
              currency: selectedService.currency || "BRL",
            }).format(selectedService.price)}{" "}
            · {selectedService.durationMinutes} min
          </p>
        )}
        {serviceId && !people.length && (
          <p className={s.bookingSummary} role="status">
            Nenhum profissional disponível para este serviço.
          </p>
        )}
        <TextField
          label="Data"
          name="date"
          type="date"
          value={date}
          onChange={(event) => {
            setDate(event.target.value);
            setTime("");
          }}
          required
        />
        {slots.isFetching && validSelection && date && <Spinner label="Consultando horários…" />}
        {slots.isError && <Alert tone="error">{bookingError(slots.error)}</Alert>}
        {slots.data && (
          <label>
            Horário
            <select value={time} onChange={(event) => setTime(event.target.value)} required>
              <option value="">
                {slots.data.slots.length ? "Selecione um horário" : "Nenhum horário disponível"}
              </option>
              {slots.data.slots.map((slot) => (
                <option key={slot} value={slot}>
                  {slot.slice(0, 5)}
                </option>
              ))}
            </select>
          </label>
        )}
        <TextField label="Nome" name="name" autoComplete="name" required maxLength={150} />
        <TextField
          label="Telefone"
          name="phone"
          type="tel"
          autoComplete="tel"
          required
          maxLength={30}
        />
        <TextField
          label="E-mail (opcional)"
          name="email"
          type="email"
          autoComplete="email"
          maxLength={200}
        />
        {book.isError && <Alert tone="error">{bookingError(book.error)}</Alert>}
        <Button type="submit" loading={book.isPending} disabled={!slots.data?.slots.includes(time)}>
          {IS_DEMO ? "Confirmar na demonstração" : "Confirmar horário"}
        </Button>
      </fieldset>
    </form>
  );
}
