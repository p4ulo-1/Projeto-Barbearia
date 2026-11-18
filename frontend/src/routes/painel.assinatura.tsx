import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { BillingPage } from "@/components/panel/BillingPage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/assinatura")({
  head: () => privateHead("Assinatura", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="assinatura">
      <BillingPage />
    </SectionGuard>
  ),
});
