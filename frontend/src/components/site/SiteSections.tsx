import type { GeneratedSite } from "@/lib/site-templates";
import { TenantGallery } from "./TenantGallery";
import { required } from "@/lib/site-schema";
import type { CSSProperties, ReactNode } from "react";
import type { Section } from "@/lib/api/contracts";
import type { PublicProfessional, PublicService } from "@/lib/api/public-site";
import s from "./styles";

function records(value: string | undefined): unknown[] {
  if (!value) return [];
  try {
    const result: unknown = JSON.parse(value);
    return Array.isArray(result) ? result : [];
  } catch {
    return [];
  }
}

function safeUrl(value?: string) {
  if (!value) return undefined;
  if (/^#[\w-]+$/.test(value) || /^\/(?![/\\])/.test(value)) return value;
  try {
    const url = new URL(value);
    return ["http:", "https:"].includes(url.protocol) ? url.href : undefined;
  } catch {
    return undefined;
  }
}
function image(value: unknown) {
  return typeof value === "string" ? safeUrl(value) : undefined;
}
function settings(theme: unknown): Record<string, unknown> {
  if (typeof theme === "string")
    return Object.fromEntries(
      theme.split(";").flatMap((entry) => {
        const [key, value] = entry.trim().split("=");
        return key && value ? [[key.trim().toLowerCase(), value.trim()]] : [];
      }),
    );
  if (theme && typeof theme === "object")
    return Object.fromEntries(
      Object.entries(theme).map(([key, value]) => [key.toLowerCase(), value]),
    );
  return {};
}

function themeStyle(theme: unknown): CSSProperties {
  const values = settings(theme);
  const primary = values["primary"] ?? values["primarycolor"];
  const secondary = values["secondary"] ?? values["secondarycolor"];
  const font = values["font"] ?? values["fontfamily"];
  const css: Record<string, string> = {};
  if (typeof primary === "string" && /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(primary))
    css["--sv-site-primary"] = primary;
  if (typeof secondary === "string" && /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i.test(secondary))
    css["--sv-site-secondary"] = secondary;
  if (typeof font === "string" && /^[a-zA-Z0-9 -]{1,60}$/.test(font))
    css["fontFamily"] = `"${font}", var(--sv-font-body)`;
  return css;
}

interface Props {
  sections: Section[];
  theme?: unknown;
  generated?: GeneratedSite;
  services?: PublicService[];
  professionals?: PublicProfessional[];
  booking?: (configuration: Record<string, string>) => ReactNode;
}

export function SiteSections({
  sections,
  theme,
  services,
  professionals,
  booking,
  generated,
}: Props) {
  let bookingRendered = false;
  return (
    <div className={s.publicSections} style={themeStyle(theme)}>
      {[...sections]
        .sort((a, b) => (a.order ?? a.sortOrder ?? 0) - (b.order ?? b.sortOrder ?? 0))
        .map(({ componentType: type, configuration: config }, index) => {
          if (!(type in required)) return null;
          const title = config["title"];
          const src = type === "Hero" && generated ? generated.banner : image(config["imageUrl"]);
          const target = safeUrl(config["ctaTarget"]);
          const cta =
            target && config["ctaLabel"] ? (
              <a className={s.publicCta} href={target}>
                {config["ctaLabel"]}
              </a>
            ) : null;
          const picture = src && (
            <img
              className={s.publicImage}
              src={src}
              alt=""
              width={960}
              height={540}
              loading={type === "Hero" ? "eager" : "lazy"}
            />
          );
          const items = records(type === "Gallery" ? config["images"] : config["items"]);
          if (type === "Team" && !professionals?.length) return null;
          const firstBooking = type === "BookingWidget" && !bookingRendered;
          if (type === "BookingWidget") bookingRendered = true;
          return (
            <section
              key={`${type}-${index}`}
              className={type === "Hero" ? s.publicHero : s.publicSection}
              data-section={type}
              data-illustrated={type === "Hero" && !!src ? "true" : undefined}
              id={
                firstBooking
                  ? "agendar"
                  : (
                      {
                        Hero: "inicio",
                        Services: "servicos",
                        Gallery: "galeria",
                        Contact: "contato",
                      } as Record<string, string>
                    )[type]
              }
            >
              {type !== "Hero" && <h2>{title}</h2>}
              {type === "Hero" && (
                <>
                  <div className={s.tenantHeroCopy}>
                    <h1>{title}</h1>
                    {config["subtitle"] && <p>{config["subtitle"]}</p>}
                    {cta}
                  </div>
                  {picture}
                </>
              )}
              {type === "About" && (
                <>
                  <p>{config["body"]}</p>
                  {picture}
                </>
              )}
              {type === "Services" && !services?.length && (
                <p>Catálogo ainda indisponível. Consulte os contatos do estabelecimento.</p>
              )}
              {type === "Services" && !!services?.length && (
                <ul className={s.publicList}>
                  {services.map((service) => (
                    <li key={service.id}>
                      <strong>{service.name}</strong>
                      {service.description && <p>{service.description}</p>}
                      {(config["showPrices"] === "true" || config["showDurations"] === "true") && (
                        <small>
                          {config["showPrices"] === "true" &&
                            new Intl.NumberFormat("pt-BR", {
                              style: "currency",
                              currency: service.currency || "BRL",
                            }).format(service.price)}
                          {config["showPrices"] === "true" &&
                            config["showDurations"] === "true" &&
                            " · "}
                          {config["showDurations"] === "true" && `${service.durationMinutes} min`}
                        </small>
                      )}
                    </li>
                  ))}
                </ul>
              )}
              {type === "Team" && professionals && (
                <ul className={s.publicList}>
                  {professionals.map((professional) => (
                    <li key={professional.id}>{professional.name}</li>
                  ))}
                </ul>
              )}
              {type === "Gallery" && generated && <TenantGallery photos={generated.gallery} />}
              {type === "Gallery" && !generated && (
                <div className={s.publicGallery}>
                  {items.map((item, i) => {
                    const url = image(
                      typeof item === "object" && item ? (item as { url?: string }).url : item,
                    );
                    if (!url) return null;
                    const alt =
                      typeof item === "object" && item ? (item as { alt?: string }).alt : undefined;
                    return (
                      <img
                        key={i}
                        src={url}
                        alt={alt ?? ""}
                        width={480}
                        height={320}
                        loading="lazy"
                      />
                    );
                  })}
                </div>
              )}
              {type === "Testimonials" &&
                items.map((item, i) => {
                  const quote = item as { quote?: string; text?: string; author?: string } | null;
                  return quote && (quote.quote || quote.text) ? (
                    <blockquote key={i}>
                      <p>{quote.quote ?? quote.text}</p>
                      {quote.author && <cite>{quote.author}</cite>}
                    </blockquote>
                  ) : null;
                })}
              {type === "Contact" && (
                <address>
                  {config["address"]}
                  {config["address"] && <br />}
                  {config["phone"] && (
                    <a href={`tel:${config["phone"].replace(/[^\d+]/g, "")}`}>{config["phone"]}</a>
                  )}
                  {config["hours"] && <p>{config["hours"]}</p>}
                  {safeUrl(config["mapEmbedUrl"]) && (
                    <a href={safeUrl(config["mapEmbedUrl"])}>Ver mapa</a>
                  )}
                </address>
              )}
              {type === "BookingWidget" &&
                (booking ? booking(config) : <p>Agendamento pelo site publicado</p>)}
              {type === "Faq" &&
                items.map((item, i) => {
                  const faq = item as { question?: string; answer?: string } | null;
                  return faq?.question ? (
                    <details key={i}>
                      <summary>{faq.question}</summary>
                      <p>{faq.answer}</p>
                    </details>
                  ) : null;
                })}
              {type === "CallToAction" && (
                <>
                  {config["body"] && <p>{config["body"]}</p>}
                  {cta}
                </>
              )}
            </section>
          );
        })}
    </div>
  );
}
