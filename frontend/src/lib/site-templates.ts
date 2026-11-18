import type { CSSProperties } from "react";
import type { Section } from "./api/contracts";
import { segmentOptions } from "./segments";

export const siteSegments = {
  Barbershop: "Barbearia",
  HairSalon: "Salão de beleza",
  Tattoo: "Estúdio de tatuagem",
  NailStudio: "Estúdio de unhas",
} as const;
export type SiteSegment = keyof typeof siteSegments;
type Component =
  "Hero" | "About" | "Services" | "Team" | "Gallery" | "BookingWidget" | "Contact" | "CallToAction";

interface Palette {
  label: string;
  colors: [background: string, surface: string, accent: string, text: string, muted: string];
}
function palette(label: string, ...colors: Palette["colors"]): Palette {
  return { label, colors };
}
export const templates = {
  Barbershop: {
    order: ["Hero", "Services", "Team", "Gallery", "BookingWidget", "Contact", "CallToAction"],
    palettes: [
      palette("Preto e cobre", "#151412", "#25221f", "#e2a77f", "#faf3e8", "#c8b9a9"),
      palette("Preto e azul", "#131820", "#202a38", "#a5c8f4", "#f2f6fc", "#b8c8dd"),
      palette("Grafite e dourado", "#22211e", "#302e28", "#f0ca78", "#fbf4e4", "#d4c7ac"),
      palette("Claro e grafite", "#f3eee7", "#fffaf3", "#4e514c", "#202522", "#534e47"),
    ],
  },
  HairSalon: {
    order: [
      "Hero",
      "About",
      "Services",
      "Team",
      "Gallery",
      "BookingWidget",
      "Contact",
      "CallToAction",
    ],
    palettes: [
      palette("Off-white e vinho", "#faf5ed", "#fffcf6", "#792e45", "#2f2425", "#67534e"),
      palette("Bege e marrom", "#f3eadc", "#fff8ed", "#78462e", "#32251c", "#665347"),
      palette("Branco e preto", "#f8f7f4", "#ffffff", "#343231", "#1c1b19", "#55504d"),
      palette("Areia e verde escuro", "#f3ecdf", "#fffaf0", "#345b48", "#232e25", "#4c5a4c"),
    ],
  },
  Tattoo: {
    order: [
      "Hero",
      "Gallery",
      "About",
      "Team",
      "Services",
      "BookingWidget",
      "Faq",
      "Contact",
      "CallToAction",
    ],
    palettes: [
      palette("Preto e vermelho", "#151517", "#222226", "#ff9f9b", "#faf7f6", "#c4bcbe"),
      palette("Preto e branco", "#171719", "#252527", "#eeeae4", "#ffffff", "#c8c7c5"),
      palette("Grafite e roxo", "#202029", "#292935", "#d1b9f9", "#f8f3ff", "#c6bdd4"),
      palette("Preto e cobre", "#191716", "#272320", "#edac88", "#fff4e9", "#ccbaad"),
    ],
  },
  NailStudio: {
    order: [
      "Hero",
      "Gallery",
      "Services",
      "About",
      "BookingWidget",
      "Team",
      "Contact",
      "CallToAction",
    ],
    palettes: [
      palette("Rosa e branco", "#fbf1f4", "#fffbfc", "#753452", "#301f2a", "#66505d"),
      palette("Nude e marrom", "#f7eee8", "#fffaf6", "#79463d", "#342823", "#6a5249"),
      palette("Branco e ameixa", "#faf6f9", "#ffffff", "#63365b", "#2e2330", "#625263"),
      palette("Lavanda e cinza", "#f2eef8", "#fcfaff", "#5a477d", "#2b2536", "#5d536d"),
    ],
  },
} satisfies Record<SiteSegment, { order: (Component | "Faq")[]; palettes: Palette[] }>;

export interface SiteImage {
  name: string;
  src: string;
  approved: boolean;
  alt?: string;
}

export interface Draft {
  segment: SiteSegment;
  name: string;
  description: string;
  phone: string;
  showLocation: boolean;
  address: string;
  palette: number;
  logo?: SiteImage | undefined;
  banner?: SiteImage | undefined;
  useLogoIcon?: boolean;
  gallery: SiteImage[];
  intendedProfessionals: number;
  title: string;
  subtitle: string;
  cta: string;
  showAbout: boolean;
  showTeam: boolean;
  showGallery: boolean;
}

export interface GeneratedSite {
  name: string;
  segment: SiteSegment;
  palette: number;
  logo?: string;
  banner?: string;
  icon?: string;
  gallery: { src: string; alt: string }[];
  sections: Section[];
}

export function isSiteSegment(value: string): value is SiteSegment {
  return Object.hasOwn(siteSegments, value);
}

export function registeredSiteSegment(value: string): SiteSegment | undefined {
  if (isSiteSegment(value)) return value;
  const label = segmentOptions.find(({ code }) => String(code) === value)?.label ?? value;
  const normalized = label.trim().toLocaleLowerCase("pt-BR");
  if (normalized === "tatuagem") return "Tattoo";
  if (normalized === "nail designer") return "NailStudio";
  return (Object.keys(siteSegments) as SiteSegment[]).find(
    (segment) => siteSegments[segment].toLocaleLowerCase("pt-BR") === normalized,
  );
}

export function makeDraft(segment: SiteSegment, name: string): Draft {
  return {
    segment,
    name,
    description: "",
    phone: "",
    showLocation: false,
    address: "",
    palette: 0,
    gallery: [],
    intendedProfessionals: 1,
    title: "",
    subtitle: "",
    cta: "Agendar horário",
    showAbout: true,
    showTeam: true,
    showGallery: true,
  };
}

export function approvedImage(file?: SiteImage) {
  return file?.approved && /^data:image\/(?:png|jpeg);base64,[A-Za-z0-9+/]+=*$/.test(file.src)
    ? file.src
    : undefined;
}

export function generateSite(draft: Draft, hasTeam: boolean): GeneratedSite {
  const name = draft.name.trim();
  const description = draft.description.trim();
  const cta = draft.cta.trim() || "Agendar horário";
  const booking = { ctaLabel: cta, ctaTarget: "#agendar" };
  const logo = approvedImage(draft.logo);
  const banner = approvedImage(draft.banner);
  const gallery = draft.showGallery
    ? draft.gallery.flatMap((image) => {
        const src = approvedImage(image);
        return src ? [{ src, alt: image.alt?.trim() || "" }] : [];
      })
    : [];
  const parts: Partial<Record<Component | "Faq", Record<string, string>>> = {
    Hero: {
      title: draft.title.trim() || name || "Seu estabelecimento",
      subtitle: draft.subtitle.trim() || description,
      ...booking,
    },
    Services: { title: "Serviços", showPrices: "true", showDurations: "true" },
    BookingWidget: { title: "Escolha seu horário" },
    Contact: {
      title: "Contato",
      phone: draft.phone.trim(),
      ...(draft.showLocation && draft.address.trim() ? { address: draft.address.trim() } : {}),
    },
    CallToAction: { title: "Agende seu atendimento", ...booking },
  };
  if (draft.showAbout && description) parts.About = { title: "Sobre o espaço", body: description };
  if (draft.showTeam && hasTeam)
    parts.Team = { title: draft.segment === "Tattoo" ? "Artistas" : "Equipe" };
  if (gallery.length)
    parts.Gallery = { title: draft.segment === "Tattoo" ? "Portfólio" : "Galeria" };
  const sections = templates[draft.segment].order.flatMap((componentType, order) => {
    const configuration = parts[componentType];
    return configuration ? [{ componentType, configuration, order }] : [];
  });
  return {
    name,
    segment: draft.segment,
    palette: draft.palette,
    sections,
    gallery,
    ...(logo ? { logo, ...(draft.useLogoIcon ? { icon: logo } : {}) } : {}),
    ...(banner ? { banner } : {}),
  };
}

export function identificationProblems(draft: Draft) {
  return [
    ...(!draft.name.trim() ? ["Informe o nome público."] : []),
    ...(!draft.description.trim() ? ["Descreva seu estabelecimento."] : []),
    ...(draft.phone.replace(/\D/g, "").length < 8 ? ["Informe um telefone válido."] : []),
    ...(draft.showLocation && !draft.address.trim()
      ? ["Informe a localização que deseja exibir."]
      : []),
  ];
}

export function draftProblems(draft: Draft, serviceCount: number) {
  return [
    ...identificationProblems(draft),
    ...(!serviceCount ? ["Cadastre pelo menos um serviço ativo."] : []),
    ...(!Number.isInteger(draft.intendedProfessionals) || draft.intendedProfessionals < 1
      ? ["Informe a quantidade pretendida de profissionais."]
      : []),
    ...(!draft.cta.trim() ? ["Informe o texto do botão principal."] : []),
  ];
}

export function luminance(hex: string) {
  const rgb = hex
    .slice(1)
    .match(/../g)!
    .map((pair) => {
      const value = parseInt(pair, 16) / 255;
      return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
    });
  return rgb[0]! * 0.2126 + rgb[1]! * 0.7152 + rgb[2]! * 0.0722;
}

export function siteTokens(segment: SiteSegment, index: number): CSSProperties {
  const palettes = templates[segment].palettes;
  const [background, surface, accent, text, muted] = (palettes[index] ?? palettes[0]!).colors;
  const onAccent = luminance(accent) > 0.179 ? "#000000" : "#ffffff";
  return {
    "--site-background": background,
    "--site-surface": surface,
    "--site-accent": accent,
    "--site-text": text,
    "--site-muted": muted,
    "--site-on-accent": onAccent,
    "--site-scheme": luminance(background) > 0.35 ? "light" : "dark",
  } as CSSProperties;
}
