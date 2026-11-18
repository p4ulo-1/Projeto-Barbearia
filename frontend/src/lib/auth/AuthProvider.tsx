import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { authService, migrateDemoPasswords, profileFromToken } from "@/lib/api/auth";
import { ApiError, IS_DEMO, TOKEN_REFRESH_EVENT, UNAUTHORIZED_EVENT } from "@/lib/api/http";
import type { LoginRequestDto, RegisterRequestDto, UserDto } from "@/lib/api/types";
import { tokenStore } from "./token";

type Status = "loading" | "authenticated" | "anonymous";
interface AuthCtx {
  status: Status;
  user: UserDto | null;
  sessionExpired: boolean;
  login: (dto: LoginRequestDto, remember: boolean) => Promise<UserDto>;
  register: (dto: RegisterRequestDto) => Promise<UserDto>;
  logout: () => Promise<void>;
  refresh: () => Promise<UserDto | null>;
}
const Ctx = createContext<AuthCtx | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [status, setStatus] = useState<Status>("loading");
  const [sessionExpired, setExpired] = useState(false);
  const queryClient = useQueryClient();

  const refresh = useCallback(async () => {
    const sessionKey = () => tokenStore.session()?.scope ?? tokenStore.get();
    const active = sessionKey();
    if (!active) {
      setUser(null);
      setStatus("anonymous");
      return null;
    }
    try {
      const profile = await authService.me();
      if (active !== sessionKey()) return null;
      setUser(profile);
      setStatus("authenticated");
      return profile;
    } catch (error) {
      if (active !== sessionKey()) return null;
      if (error instanceof ApiError && error.status === 401) tokenStore.clear();
      setUser(null);
      setStatus("anonymous");
      return null;
    }
  }, []);

  useEffect(() => {
    const onUnauth = () => {
      queryClient.clear();
      setUser(null);
      setStatus("anonymous");
      setExpired(true);
    };
    const onRenew = () => {
      const tokens = tokenStore.session();
      if (!tokens) return;
      const profile = profileFromToken(tokens.accessToken);
      setUser((previous) =>
        previous ? { ...previous, role: profile.role, permissions: profile.permissions } : previous,
      );
    };
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauth);
    window.addEventListener(TOKEN_REFRESH_EVENT, onRenew);
    void migrateDemoPasswords()
      .catch(() => undefined)
      .then(refresh);
    return () => {
      window.removeEventListener(UNAUTHORIZED_EVENT, onUnauth);
      window.removeEventListener(TOKEN_REFRESH_EVENT, onRenew);
    };
  }, [queryClient, refresh]);

  const login = useCallback(
    async (dto: LoginRequestDto, remember: boolean) => {
      const response = await authService.login(dto);
      queryClient.clear();
      if (IS_DEMO) tokenStore.set(response.accessToken, remember);
      setUser(response.user);
      setStatus("authenticated");
      setExpired(false);
      return response.user;
    },
    [queryClient],
  );

  const register = useCallback(
    async (dto: RegisterRequestDto) => {
      const response = await authService.register(dto);
      queryClient.clear();
      if (IS_DEMO) tokenStore.set(response.accessToken, false);
      setUser(response.user);
      setStatus("authenticated");
      setExpired(false);
      return response.user;
    },
    [queryClient],
  );

  const logout = useCallback(async () => {
    const revoked = authService.logout();
    tokenStore.clear();
    queryClient.clear();
    setUser(null);
    setStatus("anonymous");
    await revoked;
  }, [queryClient]);

  return (
    <Ctx.Provider value={{ status, user, sessionExpired, login, register, logout, refresh }}>
      {children}
    </Ctx.Provider>
  );
}

export function useAuth() {
  const value = useContext(Ctx);
  if (!value) throw new Error("useAuth must be used inside AuthProvider");
  return value;
}

export function homeFor(user: UserDto): "/onboarding" | "/onboarding/previa" | "/painel" {
  switch (user.onboardingStatus) {
    case "published":
      return "/painel";
    case "ready":
    case "publishing":
    case "failed":
      return "/onboarding/previa";
    default:
      return "/onboarding";
  }
}
