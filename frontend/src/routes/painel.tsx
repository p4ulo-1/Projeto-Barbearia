import { createFileRoute, Outlet } from "@tanstack/react-router";
import { RequireAuth } from "@/components/auth/RequireAuth";
import { PanelLayout } from "@/components/panel/PanelLayout";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel")({
  head: () => privateHead("Painel", "Painel administrativo do seu estabelecimento no Servia."),
  component: () => <RequireAuth><PanelLayout><Outlet /></PanelLayout></RequireAuth>,
});
