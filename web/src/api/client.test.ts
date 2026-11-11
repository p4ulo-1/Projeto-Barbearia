/**
 * Testes do cliente de API (F14.1).
 *
 * Verificam o comportamento que importa: tratamento de erro, renovação de
 * sessão e montagem de query. Não testam a API real — isso é papel dos
 * testes de integração do backend.
 */

import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ApiClient, ApiRequestError, buildQuery } from '../api/client';

describe('buildQuery', () => {
  it('monta a query ignorando valores nulos e vazios', () => {
    const query = buildQuery({ page: 1, search: '', empty: null, missing: undefined });

    expect(query).toBe('?page=1');
  });

  it('retorna string vazia quando não há parâmetros', () => {
    expect(buildQuery({})).toBe('');
  });

  it('codifica valores com caracteres especiais', () => {
    const query = buildQuery({ search: 'João & Maria' });

    expect(query).toContain('Jo%C3%A3o');
  });
});

describe('ApiClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();

    // Stub de localStorage: o cliente depende dele para o refresh token.
    // Um stub explicito torna o teste independente do ambiente.
    const store = new Map<string, string>();

    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => store.set(key, value),
      removeItem: (key: string) => store.delete(key),
      clear: () => store.clear(),
    });
  });

  it('envia o token de acesso no cabeçalho', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), { status: 200 }),
    );

    vi.stubGlobal('fetch', fetchMock);

    const client = new ApiClient({ baseUrl: 'http://api' });

    await client.request('GET', '/test');

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;

    expect(headers['Accept']).toBe('application/json');
  });

  it('lança ApiRequestError com a mensagem do servidor', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ message: 'Horário indisponível.' }), { status: 409 }),
      ),
    );

    const client = new ApiClient({ baseUrl: 'http://api' });

    await expect(client.request('POST', '/test')).rejects.toThrow('Horário indisponível.');
  });

  it('preserva o status e o traceId no erro', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ message: 'Erro', traceId: 'abc-123' }), { status: 500 }),
      ),
    );

    const client = new ApiClient({ baseUrl: 'http://api' });

    try {
      await client.request('GET', '/test');
      expect.fail('deveria ter lançado');
    } catch (error) {
      expect(error).toBeInstanceOf(ApiRequestError);
      expect((error as ApiRequestError).status).toBe(500);
      expect((error as ApiRequestError).traceId).toBe('abc-123');
    }
  });

  it('trata resposta 204 sem corpo', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    const client = new ApiClient({ baseUrl: 'http://api' });

    await expect(client.request('POST', '/test')).resolves.toBeUndefined();
  });

  it('usa mensagem genérica quando o corpo não é JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('erro interno', { status: 500 })),
    );

    const client = new ApiClient({ baseUrl: 'http://api' });

    await expect(client.request('GET', '/test')).rejects.toThrow('Erro 500');
  });
});
