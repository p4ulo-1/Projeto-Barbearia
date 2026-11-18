import { createFileRoute } from "@tanstack/react-router";
import { SectionGuard } from "@/components/panel/PanelPage";
import { CatalogPage } from "@/components/panel/CatalogPage";
import { privateHead } from "@/lib/head";

export const Route = createFileRoute("/painel/servicos")({
  head: () => privateHead("Serviços", "Gerencie seu estabelecimento no Servia."),
  component: () => (
    <SectionGuard section="servicos">
      <CatalogPage />
    </SectionGuard>
  ),
});
