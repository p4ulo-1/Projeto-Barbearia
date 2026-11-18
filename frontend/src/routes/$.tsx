import { createFileRoute, notFound } from "@tanstack/react-router";
import { PublicSite } from "@/components/site/PublicSite";
import { publicHost } from "@/lib/api/public-host";

export const Route = createFileRoute("/$")({
  loader: async () => {
    const host = await publicHost();
    if (!host) throw notFound();
    return host;
  },
  component: PublicPage,
});

function PublicPage() {
  const host = Route.useLoaderData();
  const { _splat } = Route.useParams();
  return <PublicSite host={host} slug={_splat ?? ""} />;
}
