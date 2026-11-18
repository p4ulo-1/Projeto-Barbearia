import { afterEach, expect, it, vi } from "vitest";
import { realOperations } from "@/lib/api/operations";
import { tokenStore } from "@/lib/auth/token";

it("envia os contratos reais de catálogo e equipe", async () => {
  tokenStore.setSession({ accessToken: "token", refreshToken: "refresh", expiresAt: new Date(Date.now() + 900_000).toISOString() });
  const calls: { path: string; method: string; body: unknown }[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (path: string, options: RequestInit) => {
      calls.push({
        path,
        method: options.method ?? "GET",
        body: options.body ? JSON.parse(String(options.body)) : null,
      });
      return Response.json({ items: [] });
    }),
  );
  await realOperations.service("s");
  await realOperations.updateService("s", { name: "Corte", durationMinutes: 30, highlight: false });
  await realOperations.activateService("s");
  await realOperations.professionals(1, 20, true);
  await realOperations.professional("p");
  await realOperations.updatePro("p", { name: "Ana", title: null, bio: null });
  await realOperations.activatePro("p");
  await realOperations.deactivatePro("p");
  await realOperations.unassignService("p", "s");
  expect(calls.map(({ path, method }) => [method, path])).toEqual([
    ["GET", "/api/v1/catalog/services/s"],
    ["PUT", "/api/v1/catalog/services/s"],
    ["POST", "/api/v1/catalog/services/s/activate"],
    ["GET", "/api/v1/team/professionals?includeInactive=true&page=1&pageSize=20"],
    ["GET", "/api/v1/team/professionals/p"],
    ["PUT", "/api/v1/team/professionals/p"],
    ["POST", "/api/v1/team/professionals/p/activate"],
    ["POST", "/api/v1/team/professionals/p/deactivate"],
    ["DELETE", "/api/v1/team/professionals/p/services/s"],
  ]);
  expect(calls[1]?.body).toEqual({ name: "Corte", durationMinutes: 30, highlight: false });
  expect(calls[5]?.body).toEqual({ name: "Ana", title: null, bio: null });
});
afterEach(() => { vi.unstubAllGlobals(); tokenStore.clear(); });
