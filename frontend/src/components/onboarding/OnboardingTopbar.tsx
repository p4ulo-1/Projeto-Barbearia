import { Link, useNavigate } from "@tanstack/react-router";
import { LogOut } from "lucide-react";
import { Wordmark } from "@/components/site/Wordmark";
import { DemoBadge } from "@/components/ui-sv";
import { useAuth } from "@/lib/auth/AuthProvider";
import s from "./styles";

export function OnboardingTopbar({
  progress,
  onBeforeExit,
}: {
  progress?: { current: number; total: number; label: string };
  onBeforeExit?: () => Promise<boolean>;
}) {
  const { logout, user } = useAuth();
  const navigate = useNavigate();
  const pct = progress ? Math.round((progress.current / progress.total) * 100) : 0;
  return (
    <header className={s.topbar}>
      <Link
        to="/"
        aria-label="Servia"
        onClick={async (event) => {
          if (!onBeforeExit || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
            return;
          event.preventDefault();
          if (await onBeforeExit()) await navigate({ to: "/" });
        }}
      >
        <Wordmark />
      </Link>
      {progress && (
        <div className={s.progress}>
          <div className={s.progressText}>
            <span>
              Etapa {progress.current} de {progress.total}
            </span>
            <b>{progress.label}</b>
          </div>
          <div
            className={s.progressTrack}
            role="progressbar"
            aria-valuenow={pct}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-label="Progresso da configuração"
          >
            <span style={{ width: `${pct}%` }} />
          </div>
        </div>
      )}
      <div className={s.topActions}>
        <DemoBadge />
        <span className={s.userName}>{user?.establishmentName}</span>
        <button
          className={s.iconBtn}
          onClick={async () => {
            if (onBeforeExit && !(await onBeforeExit())) return;
            await logout();
            void navigate({ to: "/entrar" });
          }}
          aria-label="Sair"
          title="Sair (seu progresso fica salvo)"
        >
          <LogOut size={18} />
        </button>
      </div>
    </header>
  );
}
