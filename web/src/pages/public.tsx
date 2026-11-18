/**
 * Página pública do site (F14.6).
 *
 * O site do tenant é acessível sem login: é o que o cliente final vê.
 */

import { useEffect, useState } from 'react';
import type { Service } from '../api/types';

interface PublicSitePageProps {
  readonly baseUrl: string;
}

/**
 * Site público do estabelecimento (F14.6).
 *
 * Renderiza o snapshot publicado. O conteúdo vem do servidor, não é
 * hardcoded: cada tenant tem o seu.
 */
export function PublicSitePage({ baseUrl }: PublicSitePageProps) {
  const [services, setServices] = useState<readonly Service[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load(): Promise<void> {
      try {
        const response = await fetch(`${baseUrl}/api/v1/catalog/services?page=1&pageSize=20`);

        if (response.ok) {
          const page = (await response.json()) as { items: readonly Service[] };

          if (!cancelled) {
            setServices(page.items);
          }
        }
      } catch {
        // Site publico degrada para conteudo vazio, nao para erro.
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [baseUrl]);

  return (
    <main className="public-site">
      <header className="hero">
        <h1>Bem-vindo</h1>
        <p>Agende seu horário em poucos passos.</p>
        <a href="/agendar" className="cta">
          Agendar agora
        </a>
      </header>

      <section aria-labelledby="services-heading">
        <h2 id="services-heading">Serviços</h2>

        {loading && <p role="status">Carregando…</p>}

        {!loading && services.length === 0 && (
          <p className="muted">Nenhum serviço disponível no momento.</p>
        )}

        {services.length > 0 && (
          <ul className="service-list">
            {services.map((service) => (
              <li key={service.id}>
                <strong>{service.name}</strong>
                <span>R$ {service.price.toFixed(2)}</span>
                <span>{service.durationMinutes} min</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
