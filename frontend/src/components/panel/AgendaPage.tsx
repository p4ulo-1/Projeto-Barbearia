import { useState } from "react";
import { Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@/components/ui-sv";
import { operations as api, type Appointment } from "@/lib/api/operations";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, RecordSelect } from "./OperationsUI";
import { field, localDate, timeField, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import s from "./styles";

const status = { Confirmed: "Confirmado", Completed: "Concluído", Cancelled: "Cancelado" };

export function AgendaPage() {
  const { user } = useAuth();
  const team = hasPermission(user, "ViewTeamAgenda") && user?.role !== "professional";
  const manage = hasPermission(user, "ManageAppointments");
  const cancel = hasPermission(user, "CancelAnyAppointment");
  const [date, setDate] = useState(localDate);
  const [selected, select] = useState("");
  const [editing, edit] = useState("");
  const [cancelling, setCancelling] = useState("");
  const appts = useQuery({
    queryKey: ["panel", "appointments", date, selected],
    queryFn: () => api.appointments(date, selected || undefined),
    enabled: !!date,
  });
  const pros = useQuery({
    queryKey: ["panel", "professionals", "options"],
    queryFn: () => api.professionals(1, 100),
    enabled: team,
  });
  const customers = useQuery({
    queryKey: ["panel", "customers", "options", ""],
    queryFn: () => api.customers(1, undefined, 100),
    enabled: hasPermission(user, "ViewCustomers"),
  });
  const services = useQuery({
    queryKey: ["panel", "services", "options"],
    queryFn: () => api.services(1, undefined, 100),
    enabled: hasPermission(user, "ViewServices"),
  });
  const names = new Map(
    [
      ...(pros.data?.items ?? []),
      ...(customers.data?.items ?? []),
      ...(services.data?.items ?? []),
    ].map((item) => [item.id, item.name]),
  );
  const op = useOperation();
  const ownId =
    user?.role === "professional" && !team ? appts.data?.[0]?.professionalId : undefined;
  return (
    <>
      <PageHeader
        title="Agenda"
        description="Consulte os atendimentos e a disponibilidade de cada profissional."
      />
      {op.feedback}
      <ModuleCard title={team ? "Agenda da equipe" : "Meus atendimentos"}>
        <div className={s.formGrid}>
          <TextField
            label="Data dos atendimentos"
            name="date"
            type="date"
            value={date}
            onChange={(event) => {
              setDate(event.target.value);
              edit("");
              setCancelling("");
            }}
          />
          {team && (
            <RecordSelect
              name="professional"
              label="Filtrar profissional"
              records={pros.data?.items ?? []}
              value={selected}
              onChange={select}
            />
          )}
        </div>
        {date && (
          <QueryState query={appts} loading="table">
            {(data) =>
              data.length ? (
                <div className={s.tableWrap}>
                  <table className={s.table}>
                    <thead>
                      <tr>
                        <th>Horário</th>
                        <th>Cliente</th>
                        <th>Serviço</th>
                        <th>Profissional</th>
                        <th>Situação</th>
                        {(manage || cancel) && <th>Ações</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {[...data]
                        .sort((a, b) => a.time.localeCompare(b.time))
                        .map((item) => (
                          <tr key={item.id}>
                            <td>
                              {item.time.slice(0, 5)} · {item.durationMinutes} min
                            </td>
                            <td>{names.get(item.customerId) ?? "Cliente"}</td>
                            <td>{names.get(item.serviceId) ?? "Serviço"}</td>
                            <td>
                              {names.get(item.professionalId) ??
                                (team ? "Profissional" : "Minha agenda")}
                            </td>
                            <td>
                              <span className={s.pill}>{status[item.status]}</span>
                            </td>
                            {(manage || cancel) && (
                              <td>
                                {item.status === "Confirmed" && (
                                  <div className={s.pageActions}>
                                    {manage && (
                                      <Button
                                        variant="ghost"
                                        onClick={() => {
                                          edit(item.id);
                                          setCancelling("");
                                        }}
                                      >
                                        Remarcar
                                      </Button>
                                    )}
                                    {manage &&
                                      new Date(item.date + "T" + item.time).getTime() <=
                                        Date.now() && (
                                        <Button
                                          variant="ghost"
                                          disabled={op.busy}
                                          onClick={() =>
                                            op.run(
                                              () => api.complete(item.id),
                                              "Atendimento concluído.",
                                            )
                                          }
                                        >
                                          Concluir
                                        </Button>
                                      )}
                                    {cancel && (
                                      <Button
                                        variant="ghost"
                                        onClick={() => {
                                          setCancelling(item.id);
                                          edit("");
                                        }}
                                      >
                                        Cancelar
                                      </Button>
                                    )}
                                  </div>
                                )}
                              </td>
                            )}
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p>Nenhum atendimento para a data selecionada.</p>
              )
            }
          </QueryState>
        )}
      </ModuleCard>
      {editing && appts.data?.find((entry) => entry.id === editing) && (
        <Reschedule
          key={editing}
          appointment={appts.data.find((entry) => entry.id === editing)!}
          availability={team}
          close={() => edit("")}
        />
      )}
      {cancelling && (
        <ModuleCard title="Cancelar agendamento">
          <p>O horário voltará a ficar disponível para outro atendimento.</p>
          <form
            className={s.formGrid}
            onSubmit={(event) => {
              event.preventDefault();
              const reason = field(new FormData(event.currentTarget), "reason");
              op.run(
                () => api.cancel(cancelling, reason),
                "Agendamento cancelado.",
                () => setCancelling(""),
              );
            }}
          >
            <TextField name="reason" label="Motivo (opcional)" maxLength={500} />
            <Button type="submit" loading={op.busy}>
              Confirmar cancelamento
            </Button>
            <Button type="button" variant="ghost" onClick={() => setCancelling("")}>
              Voltar
            </Button>
          </form>
        </ModuleCard>
      )}
      {team && (
        <ModuleCard
          id="novo-agendamento"
          title={manage ? "Novo agendamento" : "Consultar disponibilidade"}
        >
          <QueryState query={pros}>
            {(data) => (
              <QueryState query={services}>
                {(catalog) => (
                  <CreateAppointment
                    pros={data.items.filter(
                      (person) => person.isActive && person.isServiceProvider,
                    )}
                    services={catalog.items.filter((item) => item.isActive)}
                    date={date}
                    manage={manage}
                    canViewCustomers={hasPermission(user, "ViewCustomers")}
                  />
                )}
              </QueryState>
            )}
          </QueryState>
        </ModuleCard>
      )}
      {!team &&
        manage &&
        (ownId && hasPermission(user, "ViewCustomers") && hasPermission(user, "ViewServices") ? (
          <ModuleCard id="novo-agendamento" title="Novo atendimento na minha agenda">
            <QueryState query={services}>
              {(catalog) => (
                <CreateOwnAppointment
                  professionalId={ownId}
                  initialDate={date}
                  services={catalog.items.filter((service) => service.isActive)}
                />
              )}
            </QueryState>
          </ModuleCard>
        ) : (
          <Alert>
            Para criar um atendimento, consulte uma data que já tenha um compromisso seu. A agenda
            usa o profissional vinculado ao compromisso retornado pelo servidor.
          </Alert>
        ))}
    </>
  );
}

function CreateOwnAppointment({
  professionalId,
  initialDate,
  services,
}: {
  professionalId: string;
  initialDate: string;
  services: { id: string; name: string; durationMinutes: number }[];
}) {
  const [search, setSearch] = useState("");
  const customers = useQuery({
    queryKey: ["panel", "customers", "options", search],
    queryFn: () => api.customers(1, search, 100),
  });
  const op = useOperation();
  return (
    <>
      {op.feedback}
      <Alert>
        Informe um horário. O servidor verifica a disponibilidade na sua agenda ao salvar.
      </Alert>
      <form
        className={s.formGrid}
        onSubmit={(event) =>
          op.submit(
            event,
            (form) => {
              const serviceId = field(form, "serviceId");
              const service = services.find((entry) => entry.id === serviceId);
              if (!service) throw new Error("Selecione um serviço ativo.");
              return api.createAppointment({
                professionalId,
                serviceId,
                customerId: field(form, "customerId"),
                date: field(form, "date"),
                time: timeField(field(form, "time")),
              });
            },
            "Agendamento criado.",
            false,
          )
        }
      >
        <RecordSelect name="serviceId" label="Serviço" records={services} required />
        <TextField
          name="date"
          label="Data do novo agendamento"
          type="date"
          defaultValue={initialDate}
          required
        />
        <TextField name="time" label="Horário do novo agendamento" type="time" required />
        <TextField
          name="search"
          label="Buscar cliente"
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <QueryState query={customers}>
          {(data) =>
            data.items.some((entry) => entry.isActive) ? (
              <RecordSelect
                key={search}
                name="customerId"
                label="Cliente"
                records={data.items.filter((entry) => entry.isActive)}
                required
              />
            ) : (
              <p>Nenhum cliente encontrado.</p>
            )
          }
        </QueryState>
        <Button
          type="submit"
          loading={op.busy}
          disabled={!customers.data?.items.some((entry) => entry.isActive) || !services.length}
        >
          Agendar na minha agenda
        </Button>
      </form>
    </>
  );
}
function SlotSelect({
  professional,
  date,
  serviceId,
  time,
  onTime,
}: {
  professional: string;
  date: string;
  serviceId: string;
  time: string;
  onTime: (value: string) => void;
}) {
  const query = useQuery({
    queryKey: ["panel", "availability", professional, date, serviceId],
    queryFn: () => api.availability(professional, date, serviceId),
    enabled: !!professional && !!date && !!serviceId,
  });
  if (!professional || !date || !serviceId)
    return <p>Escolha o profissional, o serviço e a data para consultar os horários.</p>;
  return (
    <QueryState query={query}>
      {(data) =>
        data.availableSlots.length ? (
          <RecordSelect
            name="time"
            label="Horário disponível"
            records={data.availableSlots.map((slot) => ({ id: slot, name: slot.slice(0, 5) }))}
            value={time}
            onChange={onTime}
            required
          />
        ) : (
          <p>Nenhum horário disponível nesta data.</p>
        )
      }
    </QueryState>
  );
}

function CreateAppointment({
  pros,
  services,
  date,
  manage,
  canViewCustomers,
}: {
  pros: { id: string; name: string }[];
  services: { id: string; name: string; durationMinutes: number }[];
  date: string;
  manage: boolean;
  canViewCustomers: boolean;
}) {
  const [professionalId, setProfessional] = useState("");
  const [serviceId, setService] = useState("");
  const [time, setTime] = useState("");
  const [search, setSearch] = useState("");
  const associations = useQuery({
    queryKey: ["panel", "professional-services", professionalId],
    queryFn: () => api.professionalServices(professionalId),
    enabled: !!professionalId,
  });
  const customerQuery = useQuery({
    queryKey: ["panel", "customers", "options", search],
    queryFn: () => api.customers(1, search, 100),
    enabled: manage && canViewCustomers,
  });
  const eligible = services.filter((entry) => associations.data?.includes(entry.id));
  const op = useOperation();
  if (!pros.length || !services.length)
    return (
      <p>
        Cadastre um profissional ativo e associe um <Link to="/painel/servicos">serviço</Link> para
        consultar horários.
      </p>
    );
  return (
    <>
      {op.feedback}
      <form
        className={s.formGrid}
        onSubmit={(event) =>
          op.submit(
            event,
            (form) =>
              api.createAppointment({
                customerId: field(form, "customerId"),
                serviceId,
                professionalId,
                date,
                time,
              }),
            "Agendamento criado.",
            false,
          )
        }
      >
        <RecordSelect
          name="professionalId"
          label="Profissional"
          records={pros}
          value={professionalId}
          onChange={(value) => {
            setProfessional(value);
            setService("");
            setTime("");
          }}
          required
        />
        {professionalId && (
          <QueryState query={associations}>
            {() =>
              eligible.length ? (
                <RecordSelect
                  name="serviceId"
                  label="Serviço"
                  records={eligible}
                  value={serviceId}
                  onChange={(value) => {
                    setService(value);
                    setTime("");
                  }}
                  required
                />
              ) : (
                <p>Este profissional ainda não tem serviços ativos vinculados.</p>
              )
            }
          </QueryState>
        )}
        <SlotSelect
          professional={professionalId}
          date={date}
          serviceId={serviceId}
          time={time}
          onTime={setTime}
        />
        {manage &&
          (canViewCustomers ? (
            <>
              <TextField
                name="search"
                type="search"
                label="Buscar cliente na agenda"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
              <QueryState query={customerQuery}>
                {(data) =>
                  data.items.some((entry) => entry.isActive) ? (
                    <RecordSelect
                      key={search}
                      name="customerId"
                      label="Cliente"
                      records={data.items.filter((entry) => entry.isActive)}
                      required
                    />
                  ) : (
                    <p>
                      Nenhum cliente encontrado. <Link to="/painel/clientes">Ver clientes</Link>
                    </p>
                  )
                }
              </QueryState>
            </>
          ) : (
            <TextField name="customerId" label="ID do cliente" required />
          ))}
        {manage && (
          <Button
            type="submit"
            loading={op.busy}
            disabled={
              !serviceId ||
              !time ||
              !date ||
              (canViewCustomers && !customerQuery.data?.items.some((entry) => entry.isActive))
            }
          >
            Agendar
          </Button>
        )}
      </form>
    </>
  );
}

function Reschedule({
  appointment,
  availability,
  close,
}: {
  appointment: Appointment;
  availability: boolean;
  close: () => void;
}) {
  const [date, setDate] = useState(appointment.date);
  const [time, setTime] = useState("");
  const op = useOperation();
  return (
    <ModuleCard title="Remarcar agendamento">
      {op.feedback}
      <form
        className={s.formGrid}
        onSubmit={(event) => {
          event.preventDefault();
          op.run(
            () => api.reschedule(appointment.id, date, timeField(time)),
            "Agendamento remarcado.",
          );
        }}
      >
        <TextField
          label="Nova data"
          name="date"
          type="date"
          value={date}
          onChange={(event) => {
            setDate(event.target.value);
            setTime("");
          }}
          required
        />
        {availability ? (
          <SlotSelect
            professional={appointment.professionalId}
            date={date}
            serviceId={appointment.serviceId}
            time={time}
            onTime={setTime}
          />
        ) : (
          <TextField
            label="Novo horário"
            name="time"
            type="time"
            value={time}
            onChange={(event) => setTime(event.target.value)}
            required
          />
        )}
        <Button type="submit" disabled={!date || !time} loading={op.busy}>
          Salvar novo horário
        </Button>
        <Button type="button" variant="ghost" onClick={close}>
          Fechar
        </Button>
      </form>
    </ModuleCard>
  );
}
