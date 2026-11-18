import { useEffect, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { homeFor, useAuth } from "@/lib/auth/AuthProvider";
import { FullPageLoader } from "@/components/ui-sv";

/** Signed-in users are sent to where they belong instead of seeing login/sign-up. */
export function GuestOnly({ children }: { children: ReactNode }) {
  const { status, user } = useAuth();
  const navigate = useNavigate();
  useEffect(() => {
    if (status === "authenticated" && user) void navigate({ to: homeFor(user), replace: true });
  }, [status, user, navigate]);
  if (status === "loading") return <FullPageLoader label="Verificando acesso…" />;
  if (status === "authenticated") return <FullPageLoader label="Redirecionando…" />;
  return <>{children}</>;
}
