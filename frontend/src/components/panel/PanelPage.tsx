import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { Lock } from "lucide-react";
import { useAuth } from "@/lib/auth/AuthProvider";
import { canAccess, type PanelSection } from "@/lib/permissions";
import s from "./styles";

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className={s.pageHead}>
      <div>
        <h1 className={s.pageTitle}>{title}</h1>
        {description && <p className={s.pageDesc}>{description}</p>}
      </div>
      {actions && <div className={s.pageActions}>{actions}</div>}
    </div>
  );
}

/** Hides a section from profiles that are not allowed. The backend still enforces access. */
export function SectionGuard({
  section,
  children,
}: {
  section: PanelSection;
  children: ReactNode;
}) {
  const { user } = useAuth();
  if (canAccess(user, section)) return <>{children}</>;
  return (
    <div className={s.state}>
      <Lock size={28} aria-hidden />
      <h1 className={s.stateTitle}>Acesso restrito</h1>
      <p>
        Seu perfil não tem permissão para acessar esta área. Fale com o responsável pelo
        estabelecimento.
      </p>
      <Link to="/painel" className={s.linkBtn}>
        Voltar ao dashboard
      </Link>
    </div>
  );
}
