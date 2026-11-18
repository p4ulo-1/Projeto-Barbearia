import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { FinancePage } from "@/components/panel/FinancePage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/financeiro")({
  head: () => privateHead("Financeiro", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="financeiro">
      <FinancePage />
    </SectionGuard>
  ),
});
