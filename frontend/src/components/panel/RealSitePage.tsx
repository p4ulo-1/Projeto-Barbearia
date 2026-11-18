import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField } from "@/components/ui-sv";
import { operations as api } from "@/lib/api/operations";
import { publishedSiteUrl } from "@/lib/api/public-site";
import { ApiError } from "@/lib/api/http";
import { tenantPattern } from "@/lib/api/contracts";
import { realOnboarding } from "@/lib/api/onboarding-real";
import { useAuth } from "@/lib/auth/AuthProvider";
import { hasPermission } from "@/lib/permissions";
import { incompleteSections } from "@/lib/site-schema";
import { SiteSections } from "@/components/site/SiteSections";
import { ModuleCard, QueryState } from "./OperationsUI";
import { field, useOperation } from "./forms";
import { PageHeader } from "./PanelPage";
import s from "./styles";

export function RealSitePage() {
  const { user } = useAuth();
  const write = hasPermission(user, "ManageTenantSettings");
  const [showPreview, setPreview] = useState(false);
  const [published, setPublished] = useState<{ id: string; version: number } | null>(null);
  const site = useQuery({
    queryKey: ["panel", "site"],
    queryFn: () =>
      api.site().catch((error: unknown) => {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
      }),
  });
  const session = useQuery({
    queryKey: ["onboarding", "real"],
    queryFn: async () => (await realOnboarding.latest()) ?? null,
  });
  const preview = useQuery({
    queryKey: ["panel", "site-preview"],
    queryFn: api.sitePreview,
    enabled: !!site.data && showPreview,
  });
  const op = useOperation();

  return (
    <>
      <PageHeader
        title="Meu site"
        description="Acompanhe o rascunho, a publicação e as versões do site do estabelecimento."
      />
      {op.feedback}
      <ModuleCard title="Situação do site">
        <QueryState query={site}>
          {(data) => {
            if (!data)
              return (
                <>
                  <p>Nenhum site criado para este estabelecimento.</p>
                  {write && (
                    <Link to="/onboarding" className={s.shortcut}>
                      Configurar com a assistente
                    </Link>
                  )}
                </>
              );
            const url = data.isPublished ? publishedSiteUrl(data.subdomain) : undefined;
            return (
              <>
                <p>
                  <strong>{data.isPublished ? "Publicado" : "Rascunho"}</strong> · {data.subdomain}{" "}
                  · {data.publicationCount} publicações
                </p>
                <div className={s.pageActions}>
                  {url && (
                    <a className={s.shortcut} href={url}>
                      Abrir site público
                    </a>
                  )}
                  <Button variant="subtle" onClick={() => setPreview(!showPreview)}>
                    {showPreview ? "Ocultar prévia" : "Ver prévia"}
                  </Button>
                  {write && session.data?.status !== "Published" && (
                    <Link to="/onboarding/previa" className={s.shortcut}>
                      Revisar configuração assistida
                    </Link>
                  )}
                </div>
              </>
            );
          }}
        </QueryState>
      </ModuleCard>
      {showPreview && site.data && (
        <ModuleCard title="Prévia privada">
          <QueryState query={preview}>
            {(data) => {
              const pages = data.pages ?? [];
              const missing = incompleteSections(pages.flatMap((page) => page.sections ?? []));
              return (
                <>
                  {missing.length > 0 && (
                    <Alert tone="warning">
                      Campos obrigatórios pendentes em{" "}
                      {missing.map((item) => item.componentType).join(", ")}.
                    </Alert>
                  )}
                  {pages.length ? (
                    pages.map((page) => (
                      <div key={page.slug}>
                        <h3>{page.title}</h3>
                        <SiteSections sections={page.sections} theme={data.theme} />
                      </div>
                    ))
                  ) : (
                    <p>O snapshot não retornou páginas para visualizar.</p>
                  )}
                  {write &&
                    pages.length > 0 &&
                    missing.length === 0 &&
                    session.data?.status === "Published" && (
                      <Button
                        loading={op.busy}
                        onClick={() => {
                          if (window.confirm("Publicar a versão exibida nesta prévia?"))
                            op.run(
                              async () => setPublished(await api.publishSite()),
                              "Versão publicada.",
                            );
                        }}
                      >
                        Publicar versão revisada
                      </Button>
                    )}
                </>
              );
            }}
          </QueryState>
        </ModuleCard>
      )}
      {site.data && (
        <ModuleCard title="Versões publicadas">
          <p>
            A API informa a quantidade de publicações. Para restaurar uma versão, informe o
            identificador da publicação.
          </p>
          {published && (
            <Alert tone="success">
              Versão {published.version} · <code>{published.id}</code>
            </Alert>
          )}
          {write && site.data.publicationCount > 0 && (
            <form
              className={s.formGrid}
              onSubmit={(event) => {
                event.preventDefault();
                const identifier = field(new FormData(event.currentTarget), "publicationId");
                if (window.confirm("Restaurar esta versão do site público?"))
                  op.run(() => api.rollbackSite(identifier), "Versão restaurada.");
              }}
            >
              <TextField
                name="publicationId"
                label="ID da publicação"
                pattern={tenantPattern}
                required
              />
              <Button type="submit" variant="subtle" loading={op.busy}>
                Restaurar versão
              </Button>
            </form>
          )}
        </ModuleCard>
      )}
    </>
  );
}
