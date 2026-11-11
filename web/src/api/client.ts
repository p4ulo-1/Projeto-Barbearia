/**
 * Cliente de API tipado (F14.1).
 *
 * Centraliza autenticação, tratamento de erro e renovação de sessão. Nenhum
 * componente chama `fetch` diretamente: assim, a política de token e de erro
 * vive em um único lugar.
 *
 * O token de acesso fica em memória, não em `localStorage`: um XSS não
 * consegue lê-lo. O refresh token fica em `localStorage` porque precisa
 * sobreviver ao recarregamento — é o compromisso usual para SPA.
 */

export interface ApiError {
  readonly message: string;
  readonly traceId?: string;
  readonly status: number;
}

export class ApiRequestError extends Error {
  readonly status: number;
  readonly traceId?: string;

  constructor(error: ApiError) {
    super(error.message);
    this.name = 'ApiRequestError';
    this.status = error.status;

    if (error.traceId !== undefined) {
      this.traceId = error.traceId;
    }
  }
}

export interface AuthTokens {
  readonly accessToken: string;
  readonly expiresAt: string;
  readonly refreshToken: string;
}

export interface PagedResponse<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPrevious: boolean;
  readonly hasNext: boolean;
}

const REFRESH_TOKEN_KEY = 'servia.refreshToken';
const TENANT_KEY = 'servia.tenantId';

/** Estado de autenticação mantido em memória. */
let accessToken: string | null = null;
let accessTokenExpiresAt: Date | null = null;

export function setAccessToken(tokens: AuthTokens): void {
  accessToken = tokens.accessToken;
  accessTokenExpiresAt = new Date(tokens.expiresAt);
  localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
}

export function clearSession(): void {
  accessToken = null;
  accessTokenExpiresAt = null;
  localStorage.removeItem(REFRESH_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}

export function setTenantId(tenantId: string): void {
  localStorage.setItem(TENANT_KEY, tenantId);
}

export function getTenantId(): string | null {
  return localStorage.getItem(TENANT_KEY);
}

/** Indica se o token de acesso está próximo de expirar. */
function isTokenExpiring(): boolean {
  if (accessTokenExpiresAt === null) {
    return true;
  }

  // Renova 30 segundos antes de expirar, para evitar corrida.
  return accessTokenExpiresAt.getTime() - Date.now() < 30_000;
}

export interface ApiClientOptions {
  readonly baseUrl: string;
  /** Chamado quando a sessão não pode ser renovada. */
  readonly onSessionExpired?: () => void;
}

export class ApiClient {
  private readonly baseUrl: string;
  private readonly onSessionExpired: (() => void) | undefined;

  constructor(options: ApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, '');
    this.onSessionExpired = options.onSessionExpired;
  }

  /** Executa uma requisição autenticada. */
  async request<T>(
    method: string,
    path: string,
    body?: unknown,
  ): Promise<T> {
    await this.ensureFreshToken();

    const response = await this.send(method, path, body);

    if (response.status === 401) {
      // Token rejeitado: tenta renovar uma vez antes de desistir.
      const renewed = await this.tryRefresh();

      if (!renewed) {
        this.onSessionExpired?.();
        throw new ApiRequestError({ message: 'Sessão expirada.', status: 401 });
      }

      const retried = await this.send(method, path, body);

      return this.parse<T>(retried);
    }

    return this.parse<T>(response);
  }

  private async send(method: string, path: string, body?: unknown): Promise<Response> {
    const headers: Record<string, string> = { Accept: 'application/json' };

    if (accessToken !== null) {
      headers['Authorization'] = `Bearer ${accessToken}`;
    }

    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    return fetch(`${this.baseUrl}${path}`, {
      method,
      headers,
      ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    });
  }

  private async parse<T>(response: Response): Promise<T> {
    if (response.status === 204) {
      return undefined as T;
    }

    const text = await response.text();

    if (!response.ok) {
      let message = `Erro ${response.status}`;
      let traceId: string | undefined;

      try {
        const parsed = JSON.parse(text) as { message?: string; traceId?: string };
        message = parsed.message ?? message;
        traceId = parsed.traceId;
      } catch {
        // Corpo não-JSON: mantém a mensagem genérica.
      }

      throw new ApiRequestError({ message, status: response.status, ...(traceId !== undefined ? { traceId } : {}) });
    }

    return text.length === 0 ? (undefined as T) : (JSON.parse(text) as T);
  }

  private async ensureFreshToken(): Promise<void> {
    if (accessToken !== null && !isTokenExpiring()) {
      return;
    }

    await this.tryRefresh();
  }

  private async tryRefresh(): Promise<boolean> {
    const refreshToken = getRefreshToken();
    const tenantId = getTenantId();

    if (refreshToken === null || tenantId === null) {
      return false;
    }

    try {
      const response = await fetch(`${this.baseUrl}/api/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Tenant-Id': tenantId,
        },
        body: JSON.stringify({ refreshToken }),
      });

      if (!response.ok) {
        clearSession();
        return false;
      }

      const tokens = (await response.json()) as AuthTokens;
      setAccessToken(tokens);

      return true;
    } catch {
      return false;
    }
  }
}

/** Constrói a query string a partir de um objeto, ignorando nulos. */
export function buildQuery(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== '') {
      search.set(key, String(value));
    }
  }

  const query = search.toString();

  return query.length > 0 ? `?${query}` : '';
}
