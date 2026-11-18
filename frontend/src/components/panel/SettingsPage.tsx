import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@/components/ui-sv";
import { operations as api, daysOfWeek, type Day } from "@/lib/api/operations";
import { IS_DEMO } from "@/lib/api/http";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { ModuleCard, QueryState } from "./OperationsUI";
import { field, timeField, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import { weekLabels } from "./forms";
import { OptionalModules, WhatsAppModule } from "./OptionalModules";
import s from "./styles";

export function SettingsPage() {
  const { user } = useAuth();
  const read = hasPermission(user, "ViewTenantSettings");
  const write = hasPermission(user, "ManageTenantSettings");
  const schedule = useQuery({
    queryKey: ["panel", "schedule"],
    queryFn: api.schedule,
    enabled: read,
  });
  const op = useOperation();
  return (
    <>
      <PageHeader
        title="Configurações"
        description="Defina o expediente e acesse os módulos do estabelecimento."
      />
      {op.feedback}
      {user?.tenantId && (
        <ModuleCard title="Identificador do estabelecimento">
          <p>Use este ID para entrar no Servia com o e-mail da sua conta.</p>
          <code>{user.tenantId}</code>
        </ModuleCard>
      )}
      {read && (
        <ModuleCard title="Expediente do estabelecimento">
          <p>
            Este horário é informativo. A disponibilidade para agendar vem da jornada de cada
            profissional.
          </p>
          <QueryState query={schedule}>
            {(data) => (
              <form
                className={s.formGrid}
                onSubmit={(event) =>
                  op.submit(
                    event,
                    (form) =>
                      api.saveSchedule(
                        daysOfWeek.map((day) => {
                          const isOpen = form.has(day);
                          return {
                            dayOfWeek: day,
                            isOpen,
                            openTime: isOpen ? timeField(field(form, day + "-open")) : null,
                            closeTime: isOpen ? timeField(field(form, day + "-close")) : null,
                          };
                        }),
                      ),
                    "Expediente salvo.",
                    false,
                  )
                }
              >
                {daysOfWeek.map((day, index) => (
                  <ScheduleDay
                    key={day}
                    day={day}
                    label={weekLabels[index] ?? day}
                    data={data.days.find((entry) => entry.dayOfWeek === day)}
                    write={write}
                  />
                ))}
                {write && (
                  <Button type="submit" loading={op.busy}>
                    Salvar expediente
                  </Button>
                )}
              </form>
            )}
          </QueryState>
        </ModuleCard>
      )}
      {IS_DEMO ? (
        <Alert>Clubes, pacotes, fidelidade, avaliações e WhatsApp exigem a API do Servia.</Alert>
      ) : (
        <>
          <OptionalModules />
          <WhatsAppModule />
        </>
      )}
    </>
  );
}

function ScheduleDay({
  day,
  label,
  data,
  write,
}: {
  day: string;
  label: string;
  data: Day | undefined;
  write: boolean;
}) {
  const [open, setOpen] = useState(data?.isOpen ?? false);
  return (
    <fieldset>
      <legend>{label}</legend>
      <div className={s.inlineFields}>
        <label className={s.checkLine}>
          <input
            type="checkbox"
            name={day}
            checked={open}
            onChange={(event) => setOpen(event.target.checked)}
            disabled={!write}
          />{" "}
          {open ? "Aberto" : "Fechado"}
        </label>
        <TextField
          label="Abertura"
          name={day + "-open"}
          type="time"
          defaultValue={data?.openTime?.slice(0, 5) ?? "09:00"}
          disabled={!write || !open}
          required={open}
        />
        <TextField
          label="Fechamento"
          name={day + "-close"}
          type="time"
          defaultValue={data?.closeTime?.slice(0, 5) ?? "18:00"}
          disabled={!write || !open}
          required={open}
        />
      </div>
    </fieldset>
  );
}
