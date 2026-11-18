// DEMO ADAPTER — simulates the Servia backend in the browser while VITE_API_URL is not set.
// Data lives in localStorage and is NOT real. Remove once the APIs are integrated.
import type { OnboardingSessionDto, SiteConfigDto, UserDto } from "../types";

export interface DemoUser extends UserDto {
  passwordHash?: string;
  password?: string;
  address: string;
  document?: string | undefined;
}
export interface DemoSession extends OnboardingSessionDto {
  step: number;
  publishStartedAt?: number;
  publishedAt?: string;
  config?: SiteConfigDto;
  fileData: Record<string, string>;
}
interface Db {
  users: DemoUser[];
  sessions: Record<string, DemoSession>;
}

const KEY = "servia.demo-db";
export function load(): Db {
  try {
    return JSON.parse(localStorage.getItem(KEY) ?? "") as Db;
  } catch {
    return { users: [], sessions: {} };
  }
}
export function save(db: Db) {
  try {
    localStorage.setItem(KEY, JSON.stringify(db));
  } catch {
    throw new Error("Armazenamento de demonstração cheio. Envie arquivos menores.");
  }
}
export const delay = (ms = 500) => new Promise((r) => setTimeout(r, ms));
export const uid = () => Math.random().toString(36).slice(2, 10);
export const tokenFor = (userId: string) => `demo.${userId}`;
export const userIdFrom = (token: string | null) =>
  token?.startsWith("demo.") ? token.slice(5) : null;
