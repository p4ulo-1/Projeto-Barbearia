// The real session is scoped to this browser tab. Demo tokens carry no privileges.
const KEY = "servia.token";
const SESSION = "servia.session.v1";

export interface Tokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

type BrowserSession = Tokens & { scope: string };

export const tokenStore = {
  get(): string | null {
    if (typeof window === "undefined") return null;
    return this.session()?.accessToken ?? sessionStorage.getItem(KEY) ?? localStorage.getItem(KEY);
  },
  session(): BrowserSession | null {
    if (typeof window === "undefined") return null;
    const stored = sessionStorage.getItem(SESSION);
    try {
      return stored ? (JSON.parse(stored) as BrowserSession) : null;
    } catch {
      return null;
    }
  },
  setSession(tokens: Tokens, scope: string = crypto.randomUUID()) {
    this.clear();
    sessionStorage.setItem(SESSION, JSON.stringify({ ...tokens, scope }));
  },
  set(token: string, remember: boolean) {
    this.clear();
    (remember ? localStorage : sessionStorage).setItem(KEY, token);
  },
  clear() {
    if (typeof window === "undefined") return;
    localStorage.removeItem(KEY);
    sessionStorage.removeItem(KEY);
    sessionStorage.removeItem(SESSION);
  },
};
