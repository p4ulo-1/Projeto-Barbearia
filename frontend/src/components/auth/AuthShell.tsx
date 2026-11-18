import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { ArrowLeft, CalendarDays, Globe, LayoutDashboard } from "lucide-react";
import { Wordmark } from "@/components/site/Wordmark";
import { DemoBadge } from "@/components/ui-sv";
import s from "./styles";

const benefits = [
  {
    title: "Seu site",
    description: "Apresente os serviços e receba agendamentos online.",
    Icon: Globe,
  },
  {
    title: "Sua agenda",
    description: "Veja a disponibilidade de cada profissional.",
    Icon: CalendarDays,
  },
  {
    title: "Sua operação",
    description: "Gerencie clientes, serviços e equipe pelo painel.",
    Icon: LayoutDashboard,
  },
];

export function AuthShell({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  return (
    <div className={s.shell}>
      <div className={s.marble} aria-hidden="true">
        <div className={s.marbleCanvas}>
          <div className={s.marbleColors} />
        </div>
      </div>
      <main className={s.main}>
        <header className={s.topbar}>
          <Link to="/" aria-label="Servia, voltar ao início">
            <Wordmark />
          </Link>
          <DemoBadge />
        </header>
        <div className={s.formWrap}>
          <h1 className={s.title}>{title}</h1>
          {subtitle && <p className={s.subtitle}>{subtitle}</p>}
          {children}
        </div>
        <footer>
          <Link to="/" className={s.back}>
            <ArrowLeft size={16} aria-hidden /> Voltar ao site
          </Link>
        </footer>
      </main>
      <aside className={s.aside} aria-label="Recursos do Servia">
        <ul className={s.asideList}>
          {benefits.map(({ title, description, Icon }) => (
            <li key={title}>
              <span className={s.featureIcon}>
                <Icon size={20} aria-hidden />
              </span>
              <div>
                <strong>{title}</strong>
                <p>{description}</p>
              </div>
            </li>
          ))}
        </ul>
      </aside>
    </div>
  );
}
