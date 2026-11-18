import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { SettingsPage } from "@/components/panel/SettingsPage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/configuracoes")({
  head: () => privateHead("Configurações", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="configuracoes">
      <SettingsPage />
    </SectionGuard>
  ),
});
