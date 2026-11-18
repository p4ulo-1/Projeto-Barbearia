import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { AgendaPage } from "@/components/panel/AgendaPage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/agenda")({
  head: () => privateHead("Agenda", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="agenda">
      <AgendaPage />
    </SectionGuard>
  ),
});
