import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { CustomersPage } from "@/components/panel/CustomersPage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/clientes")({
  head: () => privateHead("Clientes", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="clientes">
      <CustomersPage />
    </SectionGuard>
  ),
});
