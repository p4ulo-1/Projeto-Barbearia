import type { Section } from "@/lib/api/contracts";

export const required: Record<string, string[]> = {
  Hero: ["title"],
  About: ["title", "body"],
  Services: ["title"],
  Team: ["title"],
  Gallery: ["title"],
  Testimonials: ["title"],
  Contact: ["title", "address", "phone"],
  BookingWidget: ["title"],
  Faq: ["title"],
  CallToAction: ["title", "ctaLabel"],
};

export function incompleteSections(sections: Section[]) {
  return sections.filter(({ componentType, configuration }) =>
    (required[componentType] ?? []).some((field) => !configuration[field]?.trim()),
  );
}
