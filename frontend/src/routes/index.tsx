import { IS_DEMO } from "@/lib/api/http";
import { PublicSite } from "@/components/site/PublicSite";
import { publicHost } from "@/lib/api/public-host";
import { createFileRoute } from "@tanstack/react-router";
import { Header } from "@/components/site/Header";
import { Hero } from "@/components/site/Hero";
import { Benefits, Features, HowItWorks, Segments } from "@/components/site/Sections";
import { Plans } from "@/components/site/Plans";
import { Faq } from "@/components/site/Faq";
import { FinalCta, Footer } from "@/components/site/Footer";

export const Route = createFileRoute("/")({
  validateSearch: (search: Record<string, unknown>): { siteDemo?: string } =>
    IS_DEMO &&
    typeof search["siteDemo"] === "string" &&
    /^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$/.test(search["siteDemo"])
      ? { siteDemo: search["siteDemo"] }
      : {},
  head: () => ({
    meta: [
      { title: "Servia — Site, agendamentos e gestão para o seu negócio" },
      {
        name: "description",
        content:
          "Personalize seu site profissional, receba agendamentos online e gerencie seu estabelecimento de serviços em uma única plataforma.",
      },
      { property: "og:title", content: "Servia — Site, agendamentos e gestão para o seu negócio" },
      {
        property: "og:description",
        content:
          "Personalize seu site profissional, receba agendamentos online e gerencie seu estabelecimento em uma única plataforma.",
      },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary_large_image" },
    ],
  }),
  loader: () => publicHost(),
  component: HomePage,
});

function HomePage() {
  const host = Route.useLoaderData();
  const demoHost = Route.useSearch().siteDemo;
  const siteHost = host ?? demoHost;
  return siteHost ? <PublicSite host={siteHost} /> : <Index />;
}

function Index() {
  return (
    <>
      <Header />
      <main>
        <Hero />
        <Benefits />
        <Features />
        <HowItWorks />
        <Segments />
        <Plans />
        <Faq />
        <FinalCta />
      </main>
      <Footer />
    </>
  );
}
