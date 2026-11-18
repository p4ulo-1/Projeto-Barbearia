import { useEffect, useMemo, useState } from 'react';
import { bookingPath, createPublicApi, publicContext } from '../api/public';
import type { PublicService } from '../api/types';

export function PublicSitePage({ baseUrl }: { readonly baseUrl: string }) {
  const host = publicContext();
  const api = useMemo(() => createPublicApi(baseUrl, host), [baseUrl, host]);
  const [services, setServices] = useState<readonly PublicService[]>([]);
  const [businessName, setBusinessName] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    setServices([]);
    setBusinessName('');
    void Promise.all([api.site(), api.services()]).then(([site, items]) => {
      if (!cancelled) { setBusinessName(site.businessName); setServices(items); }
    }).catch(() => {
      if (!cancelled) setError('Não foi possível carregar o estabelecimento. Tente novamente.');
    }).finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [api]);
  return (
    <main className="public-site">
      <header className="hero">
        <h1>{businessName || 'Estabelecimento'}</h1>
        <p>Agende seu horário em poucos passos.</p>
        {!loading && error === null && <a href={bookingPath(host)} className="cta">Agendar agora</a>}
      </header>
      <section aria-labelledby="services-heading">
        <h2 id="services-heading">Serviços</h2>
        {loading && <p role="status">Carregando…</p>}
        {error !== null && <p role="alert">{error}</p>}
        {!loading && error === null && services.length === 0 && <p>Nenhum serviço disponível no momento.</p>}
        <ul className="service-list">
          {services.map(service => <li key={service.id}>
            <strong>{service.name}</strong>
            <span>{new Intl.NumberFormat('pt-BR', { style: 'currency', currency: service.currency }).format(service.price)}</span>
            <span>{service.durationMinutes} min</span>
            <a href={bookingPath(host, service.id)}>Agendar {service.name}</a>
          </li>)}
        </ul>
      </section>
    </main>
  );
}
