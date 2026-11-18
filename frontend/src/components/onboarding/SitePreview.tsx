import { MapPin } from "lucide-react";
import type { SiteConfigDto } from "@/lib/api/types";
import s from "./styles";

/**
 * Renders a backend SiteConfigDto with a neutral base layout.
 * TODO: replace with the system's official templates (out of scope of this delivery) keyed by templateId.
 * Only data present in the config is rendered — nothing is filled in with fictitious content.
 */
export function SitePreview({ config }: { config: SiteConfigDto }) {
  const { establishment: e, sections, logoUrl, gallery, theme } = config;
  return (
    <div className={s.site}>
      <header className={s.siteHeader}>
        {logoUrl ? <img src={logoUrl} alt={`Logotipo ${e.name}`} className={s.siteLogo} /> : <b>{e.name}</b>}
        <nav>{sections.map((x) => <span key={x.key}>{x.title}</span>)}</nav>
      </header>
      <section className={s.siteHero}>
        <small>{e.segment}</small>
        <h1>{e.name}</h1>
        {e.address && <p><MapPin size={14} /> {e.address}</p>}
        <span className={s.siteCta}>Agendar horário</span>
      </section>
      {gallery.length > 0 && <div className={s.siteGallery}>{gallery.slice(0, 6).map((g, i) => <img key={i} src={g} alt="" />)}</div>}
      {sections.map((x) => (
        <section key={x.key} className={s.siteSection}><h2>{x.title}</h2><p>{x.content}</p></section>
      ))}
      {theme.styleNotes && <p className={s.siteStyleNote}>Direção visual solicitada: {theme.styleNotes}</p>}
      <footer className={s.siteFooter}>{e.name} · Site criado com Servia</footer>
    </div>
  );
}
