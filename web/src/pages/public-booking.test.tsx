import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PublicSitePage } from './public';
import { BookingWidgetPage } from './booking';

const service = { id: 's1', name: 'Corte', price: 50, currency: 'BRL', durationMinutes: 30 };
const professional = { id: 'p1', name: 'Ana', serviceIds: ['s1'] };
let container: HTMLDivElement;
let root: Root;
let urls: URL[];
let reserve: (body: Record<string, unknown>) => Promise<Response>;
let availability: (url: URL) => Promise<Response>;
const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status }));

async function settle() { await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)); }); }
async function render(node: React.ReactNode) { await act(async () => root.render(node)); await settle(); }
async function click(text: string) {
  const button = [...container.querySelectorAll('button')].find(item => item.textContent?.includes(text));
  expect(button, text).toBeTruthy();
  await act(async () => button!.click());
  await settle();
}
async function input(id: string, value: string) {
  await act(async () => {
    const field = container.querySelector<HTMLInputElement>(id)!;
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(field, value);
    field.dispatchEvent(new Event('input', { bubbles: true }));
    field.dispatchEvent(new Event('change', { bubbles: true }));
  });
  await settle();
}
async function toDetails() {
  await render(<BookingWidgetPage baseUrl="https://api.example" />);
  await click('Corte'); await click('Ana'); await click('10:00');
  await input('#booking-name', 'Cliente');
  await input('#booking-phone', '11999999999');
}
async function submitTwice() {
  await act(async () => {
    const form = container.querySelector('form')!;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  });
  await settle();
}

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  window.history.replaceState({}, '', '/site?host=barbearia.servia.app');
  container = document.createElement('div'); document.body.append(container); root = createRoot(container);
  urls = [];
  reserve = () => json({ id: 'a1', date: '2026-12-01', time: '10:00', durationMinutes: 30 }, 201);
  availability = () => json({ slots: ['10:00'] });
  vi.stubGlobal('fetch', vi.fn((address: string, init?: RequestInit) => {
    const url = new URL(address); urls.push(url);
    expect(url.pathname.startsWith('/api/v1/public/site')).toBe(true);
    expect(url.searchParams.get('host')).toBe('barbearia.servia.app');
    expect(init?.credentials).toBe('omit');
    expect(new Headers(init?.headers).has('Authorization')).toBe(false);
    if (url.pathname.endsWith('/appointments')) return reserve(JSON.parse(init!.body as string));
    if (url.pathname.endsWith('/availability')) return availability(url);
    if (url.pathname.endsWith('/professionals')) return json([professional]);
    if (url.pathname.endsWith('/services')) return json([service]);
    return json({ businessName: 'Barbearia Teste', subdomain: 'barbearia' });
  }));
});
afterEach(async () => { await act(async () => root.unmount()); container.remove(); vi.unstubAllGlobals(); });

describe('site e reserva anônima', () => {
  it('carrega o site e mantém host/serviço nos links para reservar', async () => {
    await render(<PublicSitePage baseUrl="https://api.example" />);
    expect(container.textContent).toContain('Barbearia Teste');
    expect(container.textContent).toContain('Corte');
    const link = [...container.querySelectorAll('a')].find(item => item.textContent === 'Agendar Corte')!;
    const url = new URL(link.href);
    expect(url.searchParams.get('host')).toBe('barbearia.servia.app');
    expect(url.searchParams.get('serviceId')).toBe('s1');
    window.history.replaceState({}, '', url.pathname + url.search);
    await render(<BookingWidgetPage baseUrl="https://api.example" />);
    expect(container.textContent).toContain('2. Escolha o profissional');
    expect(urls.at(-1)!.searchParams.get('serviceId')).toBe('s1');
  });
  it('distingue falha de rede de catálogo vazio', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('network'));
    await render(<PublicSitePage baseUrl="https://api.example" />);
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Não foi possível');
    expect(container.textContent).not.toContain('Nenhum serviço');
  });
  it('confirma reserva com contatos, sem duração, e impede envio simultâneo', async () => {
    let finish!: (response: Response) => void;
    const calls: Record<string, unknown>[] = [];
    reserve = body => { calls.push(body); return new Promise(resolve => { finish = resolve; }); };
    await toDetails();
    await submitTwice();
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({ serviceId: 's1', professionalId: 'p1', name: 'Cliente', phone: '11999999999' });
    expect(calls[0]).not.toHaveProperty('durationMinutes');
    await act(async () => finish(new Response(JSON.stringify({ id: 'a1', date: '2026-12-01', time: '10:00', durationMinutes: 30 }), { status: 201 })));
    await settle();
    expect(container.textContent).toContain('Agendamento confirmado');
  });
  it('409 volta aos horários, atualiza disponibilidade e preserva os contatos', async () => {
    reserve = () => json({}, 409);
    await toDetails();
    await submitTwice();
    expect(container.textContent).toContain('acabou de ser ocupado');
    expect(container.textContent).toContain('3. Escolha a data');
    expect(urls.filter(url => url.pathname.endsWith('/availability'))).toHaveLength(2);
    await click('10:00');
    expect(container.querySelector<HTMLInputElement>('#booking-name')!.value).toBe('Cliente');
  });
  it('ignora resposta antiga quando a data muda e limpa seleção ao esvaziar a data', async () => {
    await render(<BookingWidgetPage baseUrl="https://api.example" />);
    await click('Corte'); await click('Ana');
    let finish!: (response: Response) => void;
    availability = url => url.searchParams.get('date') === '2026-12-01'
      ? new Promise(resolve => { finish = resolve; }) : json({ slots: ['15:00'] });
    await input('#booking-date', '2026-12-01');
    await input('#booking-date', '2026-12-02');
    await act(async () => finish(new Response(JSON.stringify({ slots: ['09:00'] }))));
    await settle();
    expect(container.textContent).toContain('15:00');
    expect(container.textContent).not.toContain('09:00');
    await input('#booking-date', '');
    expect(container.querySelector('.slot-list')).toBeNull();
  });
  it('reconsulta ao voltar e selecionar novamente o mesmo profissional', async () => {
    await render(<BookingWidgetPage baseUrl="https://api.example" />);
    await click('Corte'); await click('Ana'); await click('Voltar'); await click('Ana');
    expect(container.textContent).toContain('10:00');
    expect(urls.filter(url => url.pathname.endsWith('/availability'))).toHaveLength(2);
  });
  it('ignora profissionais de um serviço anteriormente escolhido', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!;
    let finish!: (response: Response) => void;
    vi.mocked(fetch).mockImplementation((address, init) => {
      const url = new URL(String(address));
      if (url.pathname.endsWith('/services')) return json([service, { ...service, id: 's2', name: 'Barba' }]);
      if (url.pathname.endsWith('/professionals')) return url.searchParams.get('serviceId') === 's1'
        ? new Promise(resolve => { finish = resolve; })
        : json([{ id: 'p2', name: 'Bruno', serviceIds: ['s2'] }]);
      return original(address, init);
    });
    await render(<BookingWidgetPage baseUrl="https://api.example" />);
    await click('Corte'); await click('Voltar'); await click('Barba');
    await act(async () => finish(new Response(JSON.stringify([professional]))));
    await settle();
    expect(container.textContent).toContain('Bruno');
    expect(container.textContent).not.toContain('Ana');
  });
  it('erro ao carregar profissionais não aparece como ausência de profissionais', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation((address, init) =>
      String(address).includes('/professionals') ? Promise.reject(new TypeError('network')) : original(address, init));
    await render(<BookingWidgetPage baseUrl="https://api.example" />);
    await click('Corte');
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('carregar os profissionais');
    expect(container.textContent).not.toContain('Nenhum profissional');
  });
});
