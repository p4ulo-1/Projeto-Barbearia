/**
 * Contexto de sessão (F14.2).
 *
 * Centraliza login, logout e estado de autenticação. Os componentes leem o
 * estado daqui; nenhum guarda token por conta própria.
 */

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import {
  ApiClient,
  clearSession,
  getRefreshToken,
  getTenantId,
  setAccessToken,
  setTenantId,
  type AuthTokens,
} from '../api/client';

export interface SessionState {
  readonly isAuthenticated: boolean;
  readonly isLoading: boolean;
  readonly error: string | null;
  readonly signIn: (tenantId: string, email: string, password: string) => Promise<boolean>;
  readonly signOut: () => Promise<void>;
}

const SessionContext = createContext<SessionState | null>(null);

export interface SessionProviderProps {
  readonly baseUrl: string;
  readonly children: ReactNode;
}

export function SessionProvider({ baseUrl, children }: SessionProviderProps) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Ao carregar, tenta renovar a sessão a partir do refresh token.
  useEffect(() => {
    let cancelled = false;

    async function restore(): Promise<void> {
      const refreshToken = getRefreshToken();
      const tenantId = getTenantId();

      if (refreshToken === null || tenantId === null) {
        if (!cancelled) {
          setIsLoading(false);
        }

        return;
      }

      try {
        const response = await fetch(`${baseUrl}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantId },
          body: JSON.stringify({ refreshToken }),
        });

        if (response.ok) {
          const tokens = (await response.json()) as AuthTokens;
          setAccessToken(tokens);

          if (!cancelled) {
            setIsAuthenticated(true);
          }
        } else {
          clearSession();
        }
      } catch {
        clearSession();
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void restore();

    return () => {
      cancelled = true;
    };
  }, [baseUrl]);

  const signIn = useCallback(
    async (tenantId: string, email: string, password: string): Promise<boolean> => {
      setError(null);
      setIsLoading(true);

      try {
        const response = await fetch(`${baseUrl}/api/auth/sign-in`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ tenantId, email, password }),
        });

        if (!response.ok) {
          const body = (await response.json().catch(() => ({}))) as { message?: string };
          setError(body.message ?? 'Não foi possível entrar.');

          return false;
        }

        const tokens = (await response.json()) as AuthTokens;

        setTenantId(tenantId);
        setAccessToken(tokens);
        setIsAuthenticated(true);

        return true;
      } catch {
        setError('Falha de conexão. Tente novamente.');

        return false;
      } finally {
        setIsLoading(false);
      }
    },
    [baseUrl],
  );

  const signOut = useCallback(async (): Promise<void> => {
    const refreshToken = getRefreshToken();
    const tenantId = getTenantId();

    if (refreshToken !== null && tenantId !== null) {
      try {
        await fetch(`${baseUrl}/api/auth/sign-out`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Tenant-Id': tenantId },
          body: JSON.stringify({ refreshToken }),
        });
      } catch {
        // Falha ao revogar no servidor nao impede o logout local.
      }
    }

    clearSession();
    setIsAuthenticated(false);
  }, [baseUrl]);

  const value = useMemo<SessionState>(
    () => ({ isAuthenticated, isLoading, error, signIn, signOut }),
    [isAuthenticated, isLoading, error, signIn, signOut],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionState {
  const context = useContext(SessionContext);

  if (context === null) {
    throw new Error('useSession deve ser usado dentro de SessionProvider.');
  }

  return context;
}

/** Cria o cliente de API com o handler de sessão expirada. */
export function createApiClient(baseUrl: string, onSessionExpired: () => void): ApiClient {
  return new ApiClient({ baseUrl, onSessionExpired });
}
