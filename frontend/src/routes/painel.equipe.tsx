import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { TeamPage } from "@/components/panel/TeamPage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/equipe")({
  head: () => privateHead("Equipe", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="equipe">
      <TeamPage />
    </SectionGuard>
  ),
});
