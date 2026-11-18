import { createFileRoute, Outlet } from "@tanstack/react-router";
import { RequireAuth } from "@/components/auth/RequireAuth";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/onboarding")({
  head: () =>
    privateHead(
      "Configuração do site",
      "Crie, personalize e revise o site do seu estabelecimento.",
    ),
  component: () => (
    <RequireAuth>
      <Outlet />
    </RequireAuth>
  ),
});
