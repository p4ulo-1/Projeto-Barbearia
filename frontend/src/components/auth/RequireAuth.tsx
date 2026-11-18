import { useEffect, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { useAuth } from "@/lib/auth/AuthProvider";
import { FullPageLoader } from "@/components/ui-sv";

/** Client-side gate for private areas. The backend still authorizes every request. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  const navigate = useNavigate();
  useEffect(() => {
    if (status === "anonymous") void navigate({ to: "/entrar", replace: true });
  }, [status, navigate]);
  if (status !== "authenticated") return <FullPageLoader label="Verificando acesso…" />;
  return <>{children}</>;
}
