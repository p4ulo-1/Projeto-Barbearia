import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { PageHeader, SectionGuard } from "@/components/panel/PanelPage";
import { RealSitePage } from "@/components/panel/RealSitePage";
import { loadWorkspace } from "@/lib/site-workspace";
import { IS_DEMO } from "@/lib/api/http";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { privateHead } from "@/lib/head";
import { Alert, Spinner } from "@/components/ui-sv";
import s from "@/components/panel/styles";

export const Route = createFileRoute("/painel/site")({
  head: () => privateHead("Meu site", "Configuração e publicação do seu site."),
  component: () => (
    <SectionGuard section="site">
      <ManageSite />
    </SectionGuard>
  ),
});

function ManageSite() {
  const { user } = useAuth();
  const scope = `demo:${user?.id ?? ""}`;
  const workspace = useQuery({
    queryKey: ["site-workspace", scope],
    queryFn: async () => (await loadWorkspace(scope)) ?? null,
    enabled: IS_DEMO && !!user,
  });
  return (
    <>
      {hasPermission(user, "ManageTenantSettings") && (
        <div className={s.pageActions}>
          <Link to="/onboarding" className={s.shortcut}>
            Abrir configurador de sites
          </Link>
        </div>
      )}
      {IS_DEMO ? (
        <>
          <PageHeader title="Meu site" description="Prévia e publicação neste navegador." />
          {workspace.isPending ? (
            <Spinner />
          ) : workspace.isError ? (
            <Alert tone="error">Não foi possível abrir o rascunho.</Alert>
          ) : workspace.data?.publication ? (
            <Link to="/" search={{ siteDemo: workspace.data.publication.slug }}>
              Abrir site publicado na demonstração
            </Link>
          ) : (
            <p>
              {workspace.data
                ? "O rascunho está disponível no configurador."
                : "Crie seu site pelo configurador."}
            </p>
          )}
        </>
      ) : (
        <RealSitePage />
      )}
    </>
  );
}
