import { BarChart3, CalendarDays, CreditCard, Globe, LayoutDashboard, Scissors, Settings, UserRound, Users } from "lucide-react";
import type { PanelSection } from "@/lib/permissions";

export const NAV = [
  { section: "dashboard", to: "/painel", label: "Dashboard", icon: LayoutDashboard },
  { section: "agenda", to: "/painel/agenda", label: "Agenda", icon: CalendarDays },
  { section: "servicos", to: "/painel/servicos", label: "Serviços", icon: Scissors },
  { section: "equipe", to: "/painel/equipe", label: "Equipe", icon: Users },
  { section: "clientes", to: "/painel/clientes", label: "Clientes", icon: UserRound },
  { section: "financeiro", to: "/painel/financeiro", label: "Financeiro", icon: BarChart3 },
  { section: "site", to: "/painel/site", label: "Meu site", icon: Globe },
  { section: "assinatura", to: "/painel/assinatura", label: "Assinatura", icon: CreditCard },
  { section: "configuracoes", to: "/painel/configuracoes", label: "Configurações", icon: Settings },
] as const satisfies readonly { section: PanelSection; to: string; label: string; icon: unknown }[];
