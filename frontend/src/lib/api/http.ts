import { tokenStore, type Tokens } from "@/lib/auth/token";

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public details?: unknown,
  ) {
    super(message);
  }

  get fieldErrors(): Record<string, string> {
    const errors = (this.details as { errors?: Record<string, string[]> })?.errors ?? {};
    return Object.fromEntries(
      Object.entries(errors).map(([key, value]) => [
        key.charAt(0).toLowerCase() + key.slice(1),
        value[0] ?? "",
      ]),
    );
  }
}

export const API_BASE_URL = (import.meta.env["VITE_API_URL"] as string | undefined)
  ?.trim()
  .replace(/\/+$/, "");
export const IS_DEMO = !API_BASE_URL;
export const UNAUTHORIZED_EVENT = "servia:unauthorized";
export const TOKEN_REFRESH_EVENT = "servia:token-refresh";

type Options = RequestInit & {
  json?: unknown;
  anonymous?: boolean;
  response?: "json" | "blob" | "text";
};

const messages: Record<number, string> = {
  400: "Confira os dados e tente novamente.",
  401: "Sua sessão expirou. Entre novamente.",
  403: "Você não tem permissão para realizar esta ação.",
  404: "Recurso não encontrado.",
  409: "Os dados mudaram ou o horário está ocupado. Atualize e tente novamente.",
  413: "O arquivo e os dados do envio ultrapassam o limite aceito pelo servidor.",
  429: "Limite de solicitações atingido. Aguarde e tente novamente.",
};

function apiError(status: number, body: unknown) {
  const data = body as { message?: string; title?: string; detail?: string } | undefined;
  return new ApiError(
    data?.message ||
      messages[status] ||
      (status >= 500
        ? "Não foi possível concluir a operação no servidor."
        : data?.detail || data?.title) ||
      "Não foi possível concluir a operação.",
    status,
    body,
  );
}

async function fetchResponse(path: string, options: Options, token?: string | null) {
  if (!API_BASE_URL) throw new ApiError("API do Servia não configurada.", 0);
  const { json, anonymous, response, ...init } = options;
  const headers = new Headers(init.headers);
  if (token && !anonymous) headers.set("Authorization", `Bearer ${token}`);
  if (json !== undefined) headers.set("Content-Type", "application/json");
  try {
    return await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers,
      body: json === undefined ? (init.body ?? null) : JSON.stringify(json),
    });
  } catch (error) {
    if (init.signal?.aborted) throw error;
    throw new ApiError("Não foi possível conectar ao servidor. Verifique sua conexão.", 0);
  }
}

let renewal: { refreshToken: string; promise: Promise<Tokens> } | undefined;

function expire(accessToken: string) {
  if (tokenStore.get() !== accessToken) return;
  tokenStore.clear();
  window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
}

function renew(session: Tokens & { scope: string }) {
  if (renewal?.refreshToken === session.refreshToken) return renewal.promise;
  const promise = http<Tokens>("/api/auth/refresh", {
    method: "POST",
    anonymous: true,
    json: { refreshToken: session.refreshToken },
  })
    .then((tokens) => {
      if (tokenStore.session()?.refreshToken !== session.refreshToken)
        throw new ApiError("A sessão foi encerrada.", 401);
      tokenStore.setSession(tokens, session.scope);
      window.dispatchEvent(new Event(TOKEN_REFRESH_EVENT));
      return tokens;
    })
    .catch((error: unknown) => {
      if (error instanceof ApiError && error.status === 401) expire(session.accessToken);
      throw error;
    })
    .finally(() => {
      if (renewal?.promise === promise) renewal = undefined;
    });
  renewal = { refreshToken: session.refreshToken, promise };
  return promise;
}

export async function refreshSession() {
  const session = tokenStore.session();
  if (session && Date.parse(session.expiresAt) <= Date.now() + 30_000) await renew(session);
  return tokenStore.session();
}

export async function http<T>(path: string, options: Options = {}): Promise<T> {
  const session = options.anonymous ? null : await refreshSession();
  if (!session && !options.anonymous) throw apiError(401, undefined);
  let res = await fetchResponse(path, options, session?.accessToken);
  if (res.status === 401 && session) {
    const current = tokenStore.session();
    if (current && current.scope === session.scope) {
      const active = current.accessToken === session.accessToken ? await renew(current) : current;
      res = await fetchResponse(path, options, active.accessToken);
      if (res.status === 401) expire(active.accessToken);
    }
  }
  if (!res.ok) throw apiError(res.status, await res.json().catch(() => undefined));
  if (res.status === 204) return undefined as T;
  if (options.response === "blob") return (await res.blob()) as T;
  if (options.response === "text") return (await res.text()) as T;
  return (await res.json()) as T;
}

export async function upload<T>(
  path: string,
  form: FormData,
  onProgress: (pct: number) => void,
  signal?: AbortSignal,
) {
  const data = await http<T>(path, { method: "POST", body: form, ...(signal ? { signal } : {}) });
  onProgress(100);
  return data;
}

export function queryPath(
  path: string,
  params: Record<string, string | number | boolean | undefined>,
) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  return search.size ? `${path}?${search}` : path;
}
