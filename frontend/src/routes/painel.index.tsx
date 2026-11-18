import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import {
  CalendarDays,
  CalendarPlus,
  Clock3,
  ExternalLink,
  Inbox,
  Scissors,
  UserPlus,
  UsersRound,
  Wallet,
  WifiOff,
} from "lucide-react";
import type { ReactNode } from "react";
import { PageHeader } from "@/components/panel/PanelPage";
import { localDate } from "@/components/panel/forms";
import { fmtMoney, fmtTime, SITE_STATUS } from "@/components/panel/status";
import { Skeleton } from "@/components/ui-sv/Skeleton";
import { Button } from "@/components/ui-sv";
import { dashboardQuery } from "@/lib/api/queries";
import { IS_DEMO } from "@/lib/api/http";
import type { DashboardDto } from "@/lib/api/types";
import { useAuth } from "@/lib/auth/AuthProvider";
import { canAccess, hasPermission } from "@/lib/permissions";
import { privateHead } from "@/lib/head";
import s from "@/components/panel/styles";

export const Route = createFileRoute("/painel/")({
  head: () => privateHead("Visão geral", "Acompanhe a operação do seu estabelecimento no Servia."),
  component: Dashboard,
});

function Dashboard() {
  const { user } = useAuth();
  const query = useQuery(dashboardQuery(user, localDate()));
  const today = new Intl.DateTimeFormat("pt-BR", {
    weekday: "long",
    day: "numeric",
    month: "long",
  }).format(new Date());
  const canSchedule = canAccess(user, "agenda");

  return (
    <>
      <PageHeader
        title="Visão geral"
        description={`${user?.establishmentName ?? "Seu estabelecimento"} · ${today}`}
        actions={
          canSchedule && (
            <Link
              to="/painel/agenda"
              hash={
                hasPermission(user, "ManageAppointments") &&
                hasPermission(user, "ViewTeamAgenda") &&
                user?.role !== "professional"
                  ? "novo-agendamento"
                  : ""
              }
              className={s.primaryAction}
            >
              <CalendarPlus size={17} aria-hidden />
              {hasPermission(user, "ManageAppointments") ? "Novo agendamento" : "Abrir agenda"}
            </Link>
          )
        }
      />
      {query.isPending ? (
        <div
          className={s.kpiGrid}
          role="status"
          aria-busy="true"
          aria-label="Carregando indicadores"
        >
          {[0, 1, 2, 3].map((index) => (
            <div key={index} className={s.kpi}>
              <Skeleton className={s.kpiPlaceholderIcon} />
              <Skeleton className={s.kpiPlaceholderLabel} />
              <Skeleton className={s.kpiPlaceholderValue} />
            </div>
          ))}
        </div>
      ) : query.isError ? (
        <section className={s.block}>
          <div className={s.state}>
            <WifiOff size={28} aria-hidden />
            <h2 className={s.stateTitle}>Não foi possível carregar o painel</h2>
            <p>{query.error.message}</p>
            <Button variant="ghost" onClick={() => void query.refetch()} loading={query.isFetching}>
              Tentar novamente
            </Button>
          </div>
        </section>
      ) : (
        <DashboardContent data={query.data} />
      )}
    </>
  );
}

function Kpi({
  label,
  value,
  icon,
  tone,
}: {
  label: string;
  value: string | number | null;
  icon: ReactNode;
  tone: string;
}) {
  return (
    <div className={s.kpi}>
      <span className={`${s.kpiIcon} ${s[tone]}`}>{icon}</span>
      <dt>{label}</dt>
      <dd className={value === null ? s.kpiEmpty : s.kpiValue}>{value ?? "Indisponível"}</dd>
    </div>
  );
}

function DashboardContent({ data }: { data: DashboardDto }) {
  const { user } = useAuth();
  const next = data.upcoming?.[0];
  const site = data.site.status ? SITE_STATUS[data.site.status] : null;
  const canSchedule = canAccess(user, "agenda");
  const canSite = canAccess(user, "site");
  const canBilling = canAccess(user, "assinatura");
  return (
    <>
      <dl className={s.kpiGrid} aria-label="Indicadores da operação">
        <Kpi
          label="Agendamentos hoje"
          value={data.todayAppointments}
          icon={<CalendarDays size={19} aria-hidden />}
          tone="kpiBlue"
        />
        <Kpi
          label="Clientes cadastrados"
          value={data.clientsCount}
          icon={<UsersRound size={19} aria-hidden />}
          tone="kpiGreen"
        />
        <Kpi
          label="Faturamento no mês"
          value={data.revenue ? fmtMoney(data.revenue.amount, data.revenue.currency) : null}
          icon={<Wallet size={19} aria-hidden />}
          tone="kpiAmber"
        />
        <Kpi
          label="Próximo atendimento hoje"
          value={next ? fmtTime(next.startsAt) : data.upcoming ? "Sem horários" : null}
          icon={<Clock3 size={19} aria-hidden />}
          tone="kpiLavender"
        />
      </dl>

      <div className={s.twoCol}>
        <section className={s.block} aria-labelledby="proximos">
          <div className={s.blockHead}>
            <h2 id="proximos" className={s.blockTitle}>
              Próximos atendimentos de hoje
            </h2>
            {canSchedule && (
              <Link to="/painel/agenda" className={s.linkBtn}>
                Ver agenda
              </Link>
            )}
          </div>
          {data.upcoming?.length ? (
            <div className={s.tableWrap}>
              <table className={s.table}>
                <thead>
                  <tr>
                    <th scope="col">Horário</th>
                    <th scope="col">Cliente</th>
                    <th scope="col">Serviço</th>
                    <th scope="col">Profissional</th>
                  </tr>
                </thead>
                <tbody>
                  {data.upcoming.map((appointment) => (
                    <tr key={appointment.id}>
                      <td>{fmtTime(appointment.startsAt)}</td>
                      <td>{appointment.clientName}</td>
                      <td>{appointment.serviceName}</td>
                      <td>{appointment.professionalName}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className={s.state}>
              <Inbox size={28} aria-hidden />
              <h3 className={s.stateTitle}>
                {data.upcoming ? "Nenhum atendimento restante hoje" : "Agenda indisponível"}
              </h3>
              <p>
                {data.upcoming
                  ? "Consulte outras datas na agenda."
                  : "Os dados da agenda não estão disponíveis para este perfil ou a consulta falhou."}
              </p>
            </div>
          )}
        </section>
        {(canSite || canBilling) && (
          <section className={s.block} aria-labelledby="conta">
            <div className={s.blockHead}>
              <h2 className={s.blockTitle} id="conta">
                Site e assinatura
              </h2>
            </div>
            <div className={s.blockBody}>
              <dl className={s.dl}>
                {canSite && (
                  <>
                    <dt>Site</dt>
                    <dd>
                      {site ? (
                        <span className={`${s.pill} ${s[site.tone] ?? ""}`}>
                          {IS_DEMO && data.site.status === "published"
                            ? "Prévia aprovada"
                            : site.label}
                        </span>
                      ) : (
                        "Indisponível"
                      )}
                    </dd>
                  </>
                )}
                {canBilling && (
                  <>
                    <dt>Assinatura</dt>
                    <dd>
                      {data.subscription
                        ? `${data.subscription.planName} · ${data.subscription.statusLabel}`
                        : "Indisponível"}
                    </dd>
                  </>
                )}
              </dl>
              <div className={s.shortcutGroup}>
                {canSite && (
                  <Link className={s.linkBtn} to="/painel/site">
                    Gerenciar site
                  </Link>
                )}
                {canBilling && (
                  <Link className={s.linkBtn} to="/painel/assinatura">
                    Ver assinatura
                  </Link>
                )}
                {data.site.publicUrl && (
                  <a className={s.linkBtn} href={data.site.publicUrl}>
                    <ExternalLink size={15} aria-hidden /> Abrir site público
                  </a>
                )}
              </div>
            </div>
          </section>
        )}
      </div>

      {(hasPermission(user, "ManageServices") ||
        hasPermission(user, "ManageTeam") ||
        hasPermission(user, "ManageCustomers")) && (
        <section className={s.block} aria-labelledby="atalhos">
          <div className={s.blockHead}>
            <h2 className={s.blockTitle} id="atalhos">
              Ações rápidas
            </h2>
          </div>
          <div className={`${s.blockBody} ${s.shortcuts}`}>
            {hasPermission(user, "ManageServices") && (
              <Link to="/painel/servicos" hash="novo-servico" className={s.shortcut}>
                <Scissors size={16} aria-hidden /> Cadastrar serviço
              </Link>
            )}
            {hasPermission(user, "ManageTeam") && (
              <Link to="/painel/equipe" hash="novo-profissional" className={s.shortcut}>
                <UsersRound size={16} aria-hidden /> Cadastrar profissional
              </Link>
            )}
            {hasPermission(user, "ManageCustomers") && (
              <Link to="/painel/clientes" hash="novo-cliente" className={s.shortcut}>
                <UserPlus size={16} aria-hidden /> Cadastrar cliente
              </Link>
            )}
          </div>
        </section>
      )}
    </>
  );
}
