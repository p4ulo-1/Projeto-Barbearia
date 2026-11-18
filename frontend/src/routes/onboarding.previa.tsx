import { createFileRoute, Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { SiteBuilder } from "@/components/onboarding/SiteBuilder";
import { SitePreview } from "@/components/onboarding/SitePreview";
import { OnboardingTopbar } from "@/components/onboarding/OnboardingTopbar";
import { RealOnboarding } from "@/components/onboarding/RealOnboarding";
import { realOnboarding } from "@/lib/api/onboarding-real";
import { onboardingService } from "@/lib/api/onboarding";
import { useAuth } from "@/lib/auth/AuthProvider";
import { IS_DEMO } from "@/lib/api/http";
import { Alert, Button, Spinner } from "@/components/ui-sv";
import s from "@/components/onboarding/styles";

export const Route = createFileRoute("/onboarding/previa")({ component: Preview });

function Preview() {
  return IS_DEMO ? <DemoPreview /> : <ServerPreview />;
}

function ServerPreview() {
  const existing = useQuery({
    queryKey: ["onboarding", "real"],
    queryFn: async () => (await realOnboarding.latest()) ?? null,
  });
  if (existing.isPending) return <Spinner label="Carregando prévia…" />;
  if (existing.isError)
    return (
      <Alert tone="error">
        Não foi possível consultar a prévia salva no servidor.
        <Button variant="ghost" onClick={() => void existing.refetch()}>
          Tentar novamente
        </Button>
        <Link to="/onboarding">Abrir configurador</Link>
      </Alert>
    );
  if (existing.data && existing.data.status !== "Collecting") return <RealOnboarding preview />;
  return <SiteBuilder />;
}

function DemoPreview() {
  const { user } = useAuth();
  const previous = useQuery({
    queryKey: ["onboarding", "legacy-preview", user?.id],
    queryFn: onboardingService.getPreview,
    enabled: !!user && user.onboardingStatus !== "in_progress",
    retry: false,
  });
  if (previous.fetchStatus === "fetching" && previous.isPending)
    return <Spinner label="Carregando prévia…" />;
  if (!previous.data) return <SiteBuilder />;
  return (
    <>
      <OnboardingTopbar />
      <main className={s.builder}>
        <div className={s.builderHeader}>
          <h1>Prévia da configuração anterior</h1>
          <Link to="/onboarding">Abrir configurador</Link>
        </div>
        <SitePreview config={previous.data} />
      </main>
    </>
  );
}
