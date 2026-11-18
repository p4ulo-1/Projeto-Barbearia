import { loadPublishedDemo } from "@/lib/site-workspace";
import type { GeneratedSite } from "@/lib/site-templates";
import { demoPublicData } from "./operations";
import { http, IS_DEMO, ApiError, queryPath } from "./http";
import type { SitePage } from "./operations";

export interface PublicSite {
  subdomain: string;
  businessName: string;
  version: number;
  theme: string;
  pages: SitePage[];
}
export interface PublicService {
  id: string;
  name: string;
  description: string | null;
  price: number;
  currency: string;
  durationMinutes: number;
}
export interface PublicProfessional {
  id: string;
  name: string;
  serviceIds: string[];
}
export interface Booking {
  name: string;
  phone: string;
  email?: string;
  serviceId: string;
  professionalId: string;
  date: string;
  time: string;
}

const path = "/api/v1/public/site";
export const realPublicSite = {
  site: (host: string) => http<PublicSite>(queryPath(path, { host }), { anonymous: true }),
  services: (host: string) =>
    http<PublicService[]>(queryPath(path + "/services", { host }), { anonymous: true }),
  professionals: (host: string) =>
    http<PublicProfessional[]>(queryPath(path + "/professionals", { host }), { anonymous: true }),
  availability: (host: string, serviceId: string, professionalId: string, date: string) =>
    http<{ slots: string[] }>(
      queryPath(path + "/availability", { host, serviceId, professionalId, date }),
      { anonymous: true },
    ),
  book: (host: string, booking: Booking) =>
    http<{ id: string; date: string; time: string; durationMinutes: number }>(
      queryPath(path + "/appointments", { host }),
      { anonymous: true, method: "POST", json: booking },
    ),
};

async function publishedDemo(host: string) {
  const entry = await loadPublishedDemo(host);
  if (!entry) throw new ApiError("Site de demonstração indisponível neste navegador.", 404);
  return entry;
}

export const demoPublicSite = {
  async site(host: string): Promise<PublicSite & { generated: GeneratedSite }> {
    const { site } = await publishedDemo(host);
    return {
      subdomain: host,
      businessName: site.name,
      version: 1,
      theme: "",
      pages: [{ slug: "", title: site.name, sections: site.sections }],
      generated: site,
    };
  },
  async services(host: string) {
    return demoPublicData.services((await publishedDemo(host)).owner);
  },
  async professionals(host: string) {
    return demoPublicData.professionals((await publishedDemo(host)).owner);
  },
  async availability(host: string, serviceId: string, professionalId: string, date: string) {
    return demoPublicData.availability(
      (await publishedDemo(host)).owner,
      serviceId,
      professionalId,
      date,
    );
  },
  async book(host: string, booking: Booking) {
    return demoPublicData.book((await publishedDemo(host)).owner, booking);
  },
} satisfies typeof realPublicSite;
export const publicSite = IS_DEMO ? demoPublicSite : realPublicSite;

const domain = (import.meta.env["VITE_PUBLIC_SITE_DOMAIN"] as string | undefined)
  ?.trim()
  .toLowerCase();
export function isPublicSiteHost(host: string) {
  return !!domain && host.toLowerCase().endsWith(`.${domain}`);
}
export function publishedSiteUrl(subdomain: string) {
  if (!domain || !/^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$/.test(subdomain)) return undefined;
  return `https://${subdomain}.${domain}/`;
}
