import type { AppointmentSummaryDto, OnboardingStatus } from "@/lib/api/types";

export const SITE_STATUS: Record<
  OnboardingStatus,
  { label: string; tone: "ok" | "warn" | "bad" | "" }
> = {
  in_progress: { label: "Em configuração", tone: "warn" },
  generating: { label: "Sendo gerado", tone: "warn" },
  ready: { label: "Aguardando aprovação", tone: "warn" },
  publishing: { label: "Publicando", tone: "warn" },
  published: { label: "Publicado", tone: "ok" },
  failed: { label: "Falha na publicação", tone: "bad" },
};

export const APPT_STATUS: Record<AppointmentSummaryDto["status"], string> = {
  scheduled: "Agendado",
  confirmed: "Confirmado",
  in_progress: "Em atendimento",
  completed: "Concluído",
  cancelled: "Cancelado",
  no_show: "Não compareceu",
};

export const fmtDateTime = (iso: string) =>
  new Date(iso).toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
export const fmtTime = (iso: string) =>
  new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
export const fmtMoney = (v: number, c: string = "BRL") =>
  v.toLocaleString("pt-BR", { style: "currency", currency: c });
