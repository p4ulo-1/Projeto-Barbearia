import { loadWorkspace } from "../site-workspace";
import { ApiError, http, IS_DEMO, refreshSession } from "./http";
import type {
  AuthResponseDto,
  LoginRequestDto,
  RegisterRequestDto,
  UserDto,
  UserRole,
} from "./types";
import { delay, load, save, tokenFor, uid, userIdFrom, type DemoUser } from "./demo/db";
import { createSessionFor } from "./demo/onboarding";
import { tokenStore, type Tokens } from "@/lib/auth/token";
import { onboardingPath, type Session } from "./contracts";
import { hasPermission } from "@/lib/permissions";

const roles: Record<string, UserRole> = {
  PlatformAdmin: "platform",
  Owner: "owner",
  Administrator: "admin",
  Professional: "professional",
  Receptionist: "receptionist",
  Customer: "customer",
};
const numberedRoles = [
  "PlatformAdmin",
  "Owner",
  "Administrator",
  "Professional",
  "Receptionist",
  "Customer",
];
const roleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

export function profileFromToken(accessToken: string): UserDto {
  try {
    const part = accessToken.split(".")[1] ?? "";
    const decoded = atob(part.replaceAll("-", "+").replaceAll("_", "/"));
    const claims = JSON.parse(
      new TextDecoder().decode(Uint8Array.from(decoded, (c) => c.charCodeAt(0))),
    ) as Record<string, unknown>;
    const tenantId = claims["tenant_id"];
    const id =
      claims["sub"] ??
      claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"];
    const email =
      claims["email"] ??
      claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"];
    if (typeof id !== "string" || typeof email !== "string") throw new Error();
    const value = String(claims["role"] ?? claims[roleClaim] ?? "");
    const role = roles[numberedRoles[Number(value)] ?? value];
    const perm = claims["perm"];
    const permissions =
      perm === undefined
        ? undefined
        : [perm]
            .flat()
            .flatMap((claim) => String(claim).split(","))
            .map((claim) => claim.trim());
    return {
      id,
      ...(typeof tenantId === "string" ? { tenantId } : {}),
      email,
      name: email.split("@")[0] || email,
      establishmentName: "Seu estabelecimento",
      segment: "",
      onboardingStatus: "published",
      role,
      permissions,
    };
  } catch {
    throw new ApiError("A API retornou uma sessão inválida.", 401);
  }
}

export async function latestSession() {
  const sessions = await http<Session[]>(`${onboardingPath}/sessions`);
  return [...sessions].sort((a, b) => b.startedAt.localeCompare(a.startedAt))[0];
}

async function userFromSession(
  tokens: Tokens,
  fallback?: Pick<UserDto, "name" | "establishmentName" | "segment">,
) {
  const user = { ...profileFromToken(tokens.accessToken), ...fallback };
  if (!hasPermission(user, "ViewTenantSettings")) return user;
  try {
    const session = await latestSession();
    if (!session)
      return {
        ...user,
        onboardingStatus: user.role === "owner" ? "in_progress" : user.onboardingStatus,
      } as UserDto;
    return {
      ...user,
      establishmentName: session.businessName,
      segment: String(session.segment),
      onboardingStatus: (session.status === "Collecting"
        ? "in_progress"
        : session.status === "Published"
          ? "published"
          : "ready") as UserDto["onboardingStatus"],
    };
  } catch {
    return user;
  }
}

async function signIn({ tenantId, email, password }: LoginRequestDto) {
  const tokens = await http<Tokens>("/api/auth/sign-in", {
    anonymous: true,
    method: "POST",
    json: { tenantId, email, password },
  }).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 401)
      throw new ApiError("Estabelecimento, e-mail ou senha inválidos.", 401);
    throw error;
  });
  profileFromToken(tokens.accessToken);
  tokenStore.setSession(tokens);
  return tokens;
}

export const realAuth = {
  async register(dto: RegisterRequestDto): Promise<AuthResponseDto> {
    const { tenantId } = await http<{ tenantId: string }>("/api/v1/public/signup", {
      method: "POST",
      anonymous: true,
      json: {
        slug: dto.slug,
        name: dto.establishmentName.trim(),
        segment: segmentCode(dto.segment),
        document: dto.document,
        ownerName: dto.ownerName.trim(),
        ownerEmail: dto.email,
        ownerPassword: dto.password,
      },
    });
    try {
      const tokens = await signIn({ tenantId, email: dto.email, password: dto.password });
      const user = await userFromSession(tokens, {
        name: dto.ownerName,
        segment: dto.segment,
        establishmentName: dto.establishmentName,
      });
      return { ...tokens, user: { ...user, onboardingStatus: "in_progress" } };
    } catch {
      throw new ApiError(
        `Cadastro concluído. Entre com o ID do estabelecimento ${tenantId} e sua senha.`,
        0,
      );
    }
  },
  async login(dto: LoginRequestDto): Promise<AuthResponseDto> {
    const tokens = await signIn(dto);
    return { ...tokens, user: await userFromSession(tokens) };
  },
  async me(): Promise<UserDto> {
    const tokens = await refreshSession();
    if (!tokens) throw new ApiError("Sua sessão expirou. Entre novamente.", 401);
    return userFromSession(tokens);
  },
  async logout() {
    const tokens = tokenStore.session();
    tokenStore.clear();
    if (tokens)
      await http<void>("/api/auth/sign-out", {
        anonymous: true,
        method: "POST",
        json: { refreshToken: tokens.refreshToken },
      }).catch(() => undefined);
  },
};

export function segmentCode(value: string) {
  if (/^\d+$/.test(value) && Number.isSafeInteger(Number(value))) return Number(value);
  throw new ApiError("Informe o código de segmento fornecido pelo Servia.", 400);
}

const strip = ({ passwordHash, password, address, document, ...user }: DemoUser): UserDto => ({
  ...user,
  role: user.role ?? "owner",
});

async function hashPassword(password: string, salt: string) {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey("raw", encoder.encode(password), "PBKDF2", false, [
    "deriveBits",
  ]);
  const hash = await crypto.subtle.deriveBits(
    {
      name: "PBKDF2",
      hash: "SHA-256",
      iterations: 100_000,
      salt: encoder.encode(salt),
    },
    key,
    256,
  );
  return Array.from(new Uint8Array(hash), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

let passwordMigration: Promise<void> | undefined;

export function migrateDemoPasswords() {
  if (!IS_DEMO) return Promise.resolve();
  if (!passwordMigration)
    passwordMigration = (async () => {
      const oldUsers = load().users.filter((user) => user.password !== undefined);
      if (!oldUsers.length) return;
      const updated = await Promise.all(
        oldUsers.map(async (user) => ({
          id: user.id,
          password: user.password,
          hash: await hashPassword(user.password!, user.id),
        })),
      );
      const db = load();
      for (const entry of updated) {
        const match = db.users.find(
          (user) => user.id === entry.id && user.password === entry.password,
        );
        if (match) {
          delete match.password;
          match.passwordHash = entry.hash;
        }
      }
      save(db);
    })().finally(() => {
      passwordMigration = undefined;
    });
  return passwordMigration;
}

async function demoUserStatus(user: DemoUser): Promise<UserDto> {
  const workspace = await loadWorkspace(`demo:${user.id}`).catch(() => undefined);
  return strip(workspace?.publication ? { ...user, onboardingStatus: "published" } : user);
}

export const demoAuth: typeof realAuth = {
  async register(dto) {
    await migrateDemoPasswords();
    const id = uid();
    const passwordHash = await hashPassword(dto.password, id);
    const db = load();
    if (db.users.some((user) => user.email.toLowerCase() === dto.email.toLowerCase()))
      throw new ApiError("Já existe uma conta com este e-mail.", 409);
    const user: DemoUser = {
      id,
      name: dto.ownerName,
      email: dto.email,
      establishmentName: dto.establishmentName,
      segment: dto.segment,
      address: dto.address,
      passwordHash,
      onboardingStatus: "in_progress",
    };
    db.users.push(user);
    save(db);
    createSessionFor(user.id, user.establishmentName);
    return { accessToken: tokenFor(user.id), user: strip(user) };
  },
  async login({ email, password }) {
    await migrateDemoPasswords();
    await delay(220);
    const user = load().users.find((entry) => entry.email.toLowerCase() === email.toLowerCase());
    if (!user || (await hashPassword(password, user.id)) !== user.passwordHash)
      throw new ApiError("E-mail ou senha incorretos.", 401);
    return { accessToken: tokenFor(user.id), user: await demoUserStatus(user) };
  },
  async me() {
    const user = load().users.find((entry) => entry.id === userIdFrom(tokenStore.get()));
    if (!user) throw new ApiError("Sua sessão expirou. Entre novamente.", 401);
    return demoUserStatus(user);
  },
  async logout() {},
};

export const authService = IS_DEMO ? demoAuth : realAuth;
