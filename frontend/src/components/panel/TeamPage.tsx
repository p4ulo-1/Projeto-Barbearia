import { weekLabels } from "./forms";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@/components/ui-sv";
import { daysOfWeek, operations as api, type Professional, type Role } from "@/lib/api/operations";
import { IS_DEMO } from "@/lib/api/http";
import { tenantPattern } from "@/lib/api/contracts";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState, Pagination, RecordSelect } from "./OperationsUI";
import { field, numberField, optional, timeField, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import { fmtDateTime } from "./status";
import s from "./styles";

export function TeamPage() {
  const { user } = useAuth();
  const write = (IS_DEMO || !!user?.permissions) && hasPermission(user, "ManageTeam");
  const read = (IS_DEMO || !!user?.permissions) && hasPermission(user, "ViewTeamAgenda");
  const [page, setPage] = useState(1);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [selected, select] = useState("");
  const people = useQuery({
    queryKey: ["panel", "professionals", page, includeInactive],
    queryFn: () => api.professionals(page, 20, includeInactive),
    enabled: read,
  });
  const membership = people.data?.items.find((item) => item.id === selected);
  const op = useOperation();
  return (
    <>
      <PageHeader
        title="Equipe"
        description="Gerencie jornadas, serviços vinculados e atuação no estabelecimento."
      />
      {op.feedback}
      {read && (
        <ModuleCard title="Profissionais">
          <label className={s.checkLine}>
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(event) => {
                setIncludeInactive(event.target.checked);
                setPage(1);
                select("");
              }}
            />{" "}
            Incluir profissionais inativos
          </label>
          <QueryState query={people}>
            {(data) => (
              <>
                {data.items.length ? (
                  <ul className={s.recordList}>
                    {data.items.map((person) => (
                      <li key={person.id}>
                        <div>
                          <strong>{person.name}</strong>
                          <p>{person.isActive ? "Ativo" : "Inativo"}</p>
                          <p>
                            {person.isServiceProvider
                              ? "Presta serviços"
                              : "Sem atuação em serviços"}
                            {person.title && ` · ${person.title}`}
                          </p>
                        </div>
                        <div className={s.pageActions}>
                          <Button variant="subtle" onClick={() => select(person.id)}>
                            Ver vínculos e bloqueios
                          </Button>
                          {write && (
                            <Button
                              variant="ghost"
                              disabled={op.busy}
                              onClick={() => {
                                if (
                                  window.confirm(
                                    `${person.isActive ? "Desativar" : "Ativar"} “${person.name}”? Os vínculos serão preservados.`,
                                  )
                                )
                                  op.run(
                                    () =>
                                      person.isActive
                                        ? api.deactivatePro(person.id)
                                        : api.activatePro(person.id),
                                    person.isActive
                                      ? "Profissional desativado."
                                      : "Profissional ativado.",
                                  );
                              }}
                            >
                              {person.isActive ? "Desativar profissional" : "Ativar profissional"}
                            </Button>
                          )}
                          {write && (
                            <Button
                              variant="ghost"
                              disabled={op.busy || !person.isActive}
                              onClick={() =>
                                op.run(
                                  () => api.setProvider(person.id, !person.isServiceProvider),
                                  "Atuação atualizada.",
                                )
                              }
                            >
                              {person.isServiceProvider ? "Desativar atuação" : "Ativar atuação"}
                            </Button>
                          )}
                        </div>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>Nenhum profissional cadastrado.</p>
                )}
                <Pagination
                  data={data}
                  setPage={(next) => {
                    setPage(next);
                    select("");
                  }}
                />
              </>
            )}
          </QueryState>
        </ModuleCard>
      )}
      {membership && (
        <ProfessionalDetails
          key={membership.id}
          person={membership}
          write={write}
          close={() => select("")}
        />
      )}
      {write && (
        <ModuleCard id="novo-profissional" title="Cadastrar profissional">
          <form
            className={s.formGrid}
            onSubmit={(event) =>
              op.submit(
                event,
                (form) =>
                  api.createPro({
                    name: field(form, "name"),
                    ...optional(form, "title"),
                    ...optional(form, "bio"),
                    workDays: form.getAll("workDays").map(String),
                    startTime: timeField(field(form, "startTime")),
                    endTime: timeField(field(form, "endTime")),
                    slotIntervalMinutes: numberField(form, "slotIntervalMinutes"),
                    isServiceProvider: form.has("isServiceProvider"),
                  }),
                "Profissional cadastrado.",
              )
            }
          >
            <TextField label="Nome completo" name="name" maxLength={150} required />
            <TextField label="Cargo ou especialidade" name="title" maxLength={100} />
            <TextField label="Bio" name="bio" maxLength={1000} />
            <TextField
              label="Início da jornada"
              name="startTime"
              type="time"
              defaultValue="09:00"
              required
            />
            <TextField
              label="Fim da jornada"
              name="endTime"
              type="time"
              defaultValue="18:00"
              required
            />
            <TextField
              label="Intervalo da agenda (min)"
              name="slotIntervalMinutes"
              type="number"
              min="5"
              max="120"
              defaultValue="30"
              required
            />
            <fieldset>
              <legend>Dias de trabalho</legend>
              <div className={s.pageActions}>
                {daysOfWeek.map((day, index) => (
                  <label className={s.checkLine} key={day}>
                    <input
                      type="checkbox"
                      name="workDays"
                      value={day}
                      defaultChecked={index >= 1 && index <= 5}
                    />
                    {weekLabels[index]}
                  </label>
                ))}
              </div>
            </fieldset>
            <label className={s.checkLine}>
              <input type="checkbox" name="isServiceProvider" defaultChecked /> Presta serviços
            </label>
            <Button type="submit" loading={op.busy}>
              Cadastrar profissional
            </Button>
          </form>
        </ModuleCard>
      )}
      {write && !IS_DEMO && <CustomRoles />}
    </>
  );
}

function ProfessionalDetails({
  person,
  write,
  close,
}: {
  person: Professional;
  write: boolean;
  close: () => void;
}) {
  const { user } = useAuth();
  const [selectedBlock, selectBlock] = useState("");
  const detail = useQuery({
    queryKey: ["panel", "professional", person.id],
    queryFn: () => api.professional(person.id),
  });
  const items = useQuery({
    queryKey: ["panel", "professional-services", person.id],
    queryFn: () => api.professionalServices(person.id),
  });
  const blocks = useQuery({
    queryKey: ["panel", "blocks", person.id],
    queryFn: () => api.blocks(person.id),
  });
  const catalog = useQuery({
    queryKey: ["panel", "services", "options"],
    queryFn: async () => {
      let result = await api.services(1, undefined, 100);
      const items = [...result.items];
      while (result.hasNext) {
        result = await api.services(result.page + 1, undefined, 100);
        items.push(...result.items);
      }
      return items;
    },
    enabled: (IS_DEMO || !!user?.permissions) && hasPermission(user, "ViewServices"),
  });
  const names = new Map((catalog.data ?? []).map((item) => [item.id, item.name]));
  const op = useOperation();
  return (
    <ModuleCard title={person.name}>
      <Button variant="ghost" onClick={close}>
        Fechar detalhes
      </Button>
      {op.feedback}
      <QueryState query={detail}>
        {(data) => (
          <>
            {write ? (
              <form
                key={data.id}
                className={s.formGrid}
                onSubmit={(event) =>
                  op.submit(
                    event,
                    (form) =>
                      api.updatePro(person.id, {
                        name: field(form, "name"),
                        title: field(form, "title") || null,
                        bio: field(form, "bio") || null,
                      }),
                    "Profissional atualizado.",
                    false,
                  )
                }
              >
                <TextField
                  label="Nome completo"
                  name="name"
                  defaultValue={data.name}
                  maxLength={150}
                  required
                />
                <TextField
                  label="Cargo ou especialidade"
                  name="title"
                  defaultValue={data.title ?? ""}
                  maxLength={100}
                />
                <TextField label="Bio" name="bio" defaultValue={data.bio ?? ""} maxLength={1000} />
                <Button type="submit" loading={op.busy}>
                  Salvar profissional
                </Button>
              </form>
            ) : (
              <p>{data.bio}</p>
            )}
          </>
        )}
      </QueryState>
      <ProfessionalScheduleEditor professionalId={person.id} write={write} />
      <h3>Serviços vinculados</h3>
      <QueryState query={items}>
        {(data) =>
          data.length ? (
            <ul className={s.recordList}>
              {data.map((item) => (
                <li key={item}>
                  {names.get(item) ?? item}
                  {write && (
                    <Button
                      variant="ghost"
                      disabled={op.busy}
                      onClick={() =>
                        op.run(() => api.unassignService(person.id, item), "Serviço desvinculado.")
                      }
                    >
                      Desvincular serviço {names.get(item) ?? item}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p>Nenhum serviço vinculado.</p>
          )
        }
      </QueryState>
      {write && catalog.data && items.data && (
        <form
          className={s.formGrid}
          onSubmit={(event) =>
            op.submit(
              event,
              (data) => api.assignService(person.id, field(data, "serviceId")),
              "Serviço vinculado.",
            )
          }
        >
          <RecordSelect
            name="serviceId"
            label="Serviço a vincular"
            records={catalog.data.filter((item) => item.isActive && !items.data?.includes(item.id))}
            required
          />
          <Button type="submit" variant="subtle" loading={op.busy}>
            Vincular serviço
          </Button>
        </form>
      )}
      <h3>Bloqueios de horário</h3>
      <QueryState query={blocks}>
        {(data) =>
          data.length ? (
            <ul className={s.recordList}>
              {data.map((item) => (
                <li key={item.id}>
                  {fmtDateTime(item.start)} a {fmtDateTime(item.end)}{" "}
                  {item.reason && `· ${item.reason}`}
                  <Button variant="ghost" onClick={() => selectBlock(item.id)}>
                    Ver detalhes do bloqueio
                  </Button>
                  {write && (
                    <Button
                      variant="ghost"
                      disabled={op.busy}
                      onClick={() => {
                        if (window.confirm("Remover este bloqueio?"))
                          op.run(
                            () => api.deleteBlock(person.id, item.id),
                            "Bloqueio removido.",
                            () => selectBlock(""),
                          );
                      }}
                    >
                      Remover bloqueio
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          ) : (
            <p>Nenhum horário bloqueado.</p>
          )
        }
      </QueryState>
      {selectedBlock && (
        <BlockDetails
          professionalId={person.id}
          blockId={selectedBlock}
          close={() => selectBlock("")}
        />
      )}
      {write && (
        <form
          className={s.formGrid}
          onSubmit={(event) =>
            op.submit(
              event,
              (data) =>
                api.block(person.id, {
                  start: new Date(field(data, "start")).toISOString(),
                  end: new Date(field(data, "end")).toISOString(),
                  ...optional(data, "reason"),
                }),
              "Horário bloqueado.",
            )
          }
        >
          <TextField name="start" label="Início do bloqueio" type="datetime-local" required />
          <TextField name="end" label="Fim do bloqueio" type="datetime-local" required />
          <TextField name="reason" label="Motivo" maxLength={300} />
          <Button type="submit" variant="subtle" loading={op.busy}>
            Bloquear horário
          </Button>
        </form>
      )}
    </ModuleCard>
  );
}

function ProfessionalScheduleEditor({
  professionalId,
  write,
}: {
  professionalId: string;
  write: boolean;
}) {
  const schedule = useQuery({
    queryKey: ["panel", "professional-schedule", professionalId],
    queryFn: () => api.professionalSchedule(professionalId),
  });
  const [revision, setRevision] = useState(0);
  const op = useOperation();
  return (
    <section>
      <h3>Jornada individual</h3>
      <p>Alterar a jornada não modifica os agendamentos existentes.</p>
      {op.feedback}
      <QueryState query={schedule}>
        {(saved) => (
          <>
            <p>
              Jornada salva:{" "}
              {saved.workDays.length
                ? saved.workDays.map((day) => weekLabels[daysOfWeek.indexOf(day)]).join(", ")
                : "Semana fechada"}{" "}
              · {saved.startTime.slice(0, 5)}–{saved.endTime.slice(0, 5)} · intervalos de{" "}
              {saved.slotIntervalMinutes} min
            </p>
            {write && (
              <form
                key={`${JSON.stringify(saved)}-${revision}`}
                className={s.formGrid}
                onSubmit={(event) =>
                  op.submit(
                    event,
                    (form) =>
                      api.updateSchedule(professionalId, {
                        workDays: form.getAll("workDays").map(String),
                        startTime: timeField(field(form, "startTime")),
                        endTime: timeField(field(form, "endTime")),
                        slotIntervalMinutes: numberField(form, "slotIntervalMinutes"),
                      }),
                    "Jornada atualizada.",
                    false,
                  )
                }
              >
                <fieldset>
                  <legend>Dias da jornada</legend>
                  <div className={s.pageActions}>
                    {daysOfWeek.map((day, index) => (
                      <label className={s.checkLine} key={day}>
                        <input
                          type="checkbox"
                          name="workDays"
                          value={day}
                          defaultChecked={saved.workDays.includes(day)}
                        />
                        {weekLabels[index]}
                      </label>
                    ))}
                  </div>
                </fieldset>
                <TextField
                  label="Início da jornada individual"
                  name="startTime"
                  type="time"
                  defaultValue={saved.startTime.slice(0, 5)}
                  required
                />
                <TextField
                  label="Fim da jornada individual"
                  name="endTime"
                  type="time"
                  defaultValue={saved.endTime.slice(0, 5)}
                  required
                />
                <TextField
                  label="Intervalo da jornada (min)"
                  name="slotIntervalMinutes"
                  type="number"
                  min="5"
                  max="120"
                  defaultValue={saved.slotIntervalMinutes}
                  required
                />
                <Button type="submit" loading={op.busy}>
                  Salvar jornada
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  disabled={op.busy}
                  onClick={() =>
                    void schedule.refetch().then(() => setRevision((value) => value + 1))
                  }
                >
                  Recuperar jornada salva
                </Button>
              </form>
            )}
          </>
        )}
      </QueryState>
    </section>
  );
}

function BlockDetails({
  professionalId,
  blockId,
  close,
}: {
  professionalId: string;
  blockId: string;
  close: () => void;
}) {
  const query = useQuery({
    queryKey: ["panel", "block", professionalId, blockId],
    queryFn: () => api.blockDetail(professionalId, blockId),
  });
  return (
    <section>
      <h4>Detalhes do bloqueio</h4>
      <Button variant="ghost" onClick={close}>
        Fechar bloqueio
      </Button>
      <QueryState query={query}>
        {(block) => (
          <p>
            {fmtDateTime(block.start)} a {fmtDateTime(block.end)}
            {block.reason && ` · ${block.reason}`}
          </p>
        )}
      </QueryState>
    </section>
  );
}

function CustomRoles() {
  const { user } = useAuth();
  const [selected, select] = useState<Role | null>(null);
  const roles = useQuery({ queryKey: ["panel", "roles"], queryFn: api.roles });
  const assignable = useQuery({
    queryKey: ["panel", "assignable-permissions"],
    queryFn: api.assignable,
  });
  const op = useOperation();
  return (
    <ModuleCard title="Papéis personalizados">
      {op.feedback}
      <QueryState query={roles}>
        {(data) =>
          data.length ? (
            <ul className={s.recordList}>
              {data.map((role) => (
                <li key={role.id}>
                  <strong>{role.name}</strong>
                  <Button variant="ghost" onClick={() => select(role)}>
                    Gerenciar papel
                  </Button>
                </li>
              ))}
            </ul>
          ) : (
            <p>Nenhum papel personalizado.</p>
          )
        }
      </QueryState>
      <QueryState query={assignable}>
        {(permissions) => (
          <form
            key={selected?.id ?? "new"}
            className={s.formGrid}
            onSubmit={(event) =>
              op.submit(
                event,
                (data) =>
                  api.saveRole(
                    {
                      name: field(data, "name"),
                      permissions: data.getAll("permissions").map(String),
                    },
                    selected?.id,
                  ),
                "Papel salvo.",
                !selected,
              )
            }
          >
            <TextField
              name="name"
              label="Nome do papel"
              defaultValue={selected?.name}
              maxLength={100}
              required
            />
            <fieldset>
              <legend>Permissões</legend>
              <div className={s.pageActions}>
                {permissions.map((permission) => (
                  <label key={permission} className={s.checkLine}>
                    <input
                      type="checkbox"
                      name="permissions"
                      value={permission}
                      defaultChecked={selected?.permissions.includes(permission)}
                    />
                    {permission}
                  </label>
                ))}
              </div>
            </fieldset>
            <Button loading={op.busy} type="submit">
              {selected ? "Atualizar papel" : "Criar papel"}
            </Button>
            {selected && (
              <Button type="button" variant="ghost" onClick={() => select(null)}>
                Novo papel
              </Button>
            )}
          </form>
        )}
      </QueryState>
      {selected && (
        <>
          <form
            key={selected.id}
            className={s.formGrid}
            onSubmit={(event) =>
              op.submit(
                event,
                async (data) => {
                  const userId = field(data, "userId");
                  if (userId.toLowerCase() === user?.id.toLowerCase())
                    throw new Error("Use o ID de outro usuário do estabelecimento.");
                  await api.assignRole(selected.id, userId, field(data, "action") === "assign");
                },
                "Vínculo do papel atualizado.",
              )
            }
          >
            <TextField name="userId" label="ID do usuário" pattern={tenantPattern} required />
            <RecordSelect
              label="Operação"
              name="action"
              records={[
                { id: "assign", name: "Atribuir papel" },
                { id: "remove", name: "Remover papel" },
              ]}
              required
            />
            <Button type="submit" variant="subtle" loading={op.busy}>
              Atualizar vínculo
            </Button>
          </form>
          <Alert>
            Ao excluir o papel, os usuários vinculados voltam ao seu perfil predefinido.
          </Alert>
          <Button
            variant="ghost"
            disabled={op.busy}
            onClick={() => {
              if (window.confirm(`Excluir o papel “${selected.name}”?`))
                op.run(
                  () => api.deleteRole(selected.id),
                  "Papel excluído.",
                  () => select(null),
                );
            }}
          >
            Excluir papel
          </Button>
        </>
      )}
    </ModuleCard>
  );
}
