import type { ComponentProps } from "react";
import { siteTokens, type GeneratedSite } from "@/lib/site-templates";
import { SiteSections } from "./SiteSections";
import s from "./styles";

export function TenantSite({
  site,
  ...props
}: Omit<ComponentProps<typeof SiteSections>, "sections" | "generated"> & { site: GeneratedSite }) {
  return (
    <div className={s.tenantContainer}>
      <div
        className={s.tenantSite}
        data-template={site.segment}
        style={siteTokens(site.segment, site.palette)}
      >
        <header className={s.tenantHeader}>
          <a href="#inicio" className={s.tenantBrand}>
            {site.logo && <img src={site.logo} alt="" width={48} height={48} />}
            <span>{site.name || "Seu estabelecimento"}</span>
          </a>
          <nav aria-label="Atalhos do estabelecimento">
            <a href="#servicos">Serviços</a>
            <a href="#contato">Contato</a>
          </nav>
        </header>
        <SiteSections {...props} sections={site.sections} generated={site} />
        <footer className={s.tenantFooter}>{site.name}</footer>
      </div>
    </div>
  );
}
