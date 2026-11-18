import { describe, expect, it } from "vitest";
import {
  draftProblems,
  generateSite,
  luminance,
  makeDraft,
  registeredSiteSegment,
  siteSegments,
  siteTokens,
  templates,
  type SiteSegment,
} from "@/lib/site-templates";

const segments = Object.keys(siteSegments) as SiteSegment[];
const image = {
  name: "portfólio.png",
  alt: "Detalhe do atendimento",
  src: "data:image/png;base64,YQ==",
  approved: true,
};
const mandatory = ["Hero", "Services", "BookingWidget", "Contact", "CallToAction"];

function contrast(first: string, second: string) {
  const levels = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (levels[0]! + 0.05) / (levels[1]! + 0.05);
}

describe("templates AV2", () => {
  it("resolve os quatro segmentos e preserva o cadastro de Nail designer", () => {
    for (const segment of segments) {
      expect(registeredSiteSegment(segment)).toBe(segment);
      expect(registeredSiteSegment(siteSegments[segment])).toBe(segment);
    }
    expect(registeredSiteSegment("Nail designer")).toBe("NailStudio");
    expect(registeredSiteSegment("Tatuagem")).toBe("Tattoo");
    expect(registeredSiteSegment("Outro")).toBeUndefined();
  });

  it("gera quatro composições distintas, determinísticas e completas", () => {
    const layouts = segments.map((segment) => {
      const draft = {
        ...makeDraft(segment, "Estabelecimento"),
        description: "Descrição do espaço",
        gallery: [image],
      };
      const site = generateSite(draft, true);
      expect(site).toEqual(generateSite(draft, true));
      const sections = site.sections.map((section) => section.componentType);
      expect(sections).toEqual(expect.arrayContaining(mandatory));
      expect(sections).toContain("Team");
      expect(sections).toContain("Gallery");
      expect(
        site.sections.find((section) => section.componentType === "Services")?.configuration,
      ).toEqual({
        title: "Serviços",
        showPrices: "true",
        showDurations: "true",
      });
      return sections.join("|");
    });
    expect(new Set(layouts).size).toBe(4);
  });

  it("omite dados ausentes e nunca renderiza um asset pendente ou inválido", () => {
    const pending = { ...image, approved: false };
    const draft = {
      ...makeDraft("Tattoo", "Estúdio"),
      useLogoIcon: true,
      logo: pending,
      banner: pending,
      gallery: [pending, { ...image, src: "javascript:alert(1)" }],
    };
    const site = generateSite(draft, false);
    expect(site.sections.map((section) => section.componentType)).toEqual(mandatory);
    expect(site.logo).toBeUndefined();
    expect(site.banner).toBeUndefined();
    expect(site.icon).toBeUndefined();
    expect(site.gallery).toEqual([]);
    expect(
      generateSite({ ...draft, logo: image, banner: image, gallery: [image] }, false),
    ).toMatchObject({
      logo: image.src,
      banner: image.src,
      icon: image.src,
      gallery: [{ src: image.src, alt: image.alt }],
    });
  });

  it("aceita contato sem endereço e só exibe a localização autorizada", () => {
    const draft = {
      ...makeDraft("NailStudio", "Estúdio"),
      phone: "11 99999-1234",
      description: "Atendimento personalizado",
      address: "São Paulo",
    };
    expect(draftProblems(draft, 1)).toEqual([]);
    expect(
      generateSite(draft, false).sections.find((section) => section.componentType === "Contact")
        ?.configuration,
    ).not.toHaveProperty("address");
    expect(
      generateSite({ ...draft, showLocation: true }, false).sections.find(
        (section) => section.componentType === "Contact",
      )?.configuration["address"],
    ).toBe("São Paulo");
  });

  it("mantém contraste AA nas 16 paletas, inclusive nos botões", () => {
    for (const segment of segments) {
      for (const [index, { colors }] of templates[segment].palettes.entries()) {
        const [background, surface, accent, text, muted] = colors;
        const tokens = siteTokens(segment, index) as Record<string, string>;
        expect(tokens["--site-accent"]).toBe(accent);
        for (const foreground of [text, muted, accent]) {
          expect(contrast(foreground, background)).toBeGreaterThanOrEqual(4.5);
          expect(contrast(foreground, surface)).toBeGreaterThanOrEqual(4.5);
        }
        expect(contrast(tokens["--site-on-accent"]!, accent)).toBeGreaterThanOrEqual(4.5);
      }
    }
  });
});
