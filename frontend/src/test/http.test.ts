import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

describe("Servia HTTP contract", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv("VITE_API_URL", "https://api.example.test/");
    sessionStorage.clear();
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("uses the same-origin API by default without falling back to demo data", async () => {
    vi.stubEnv("VITE_API_URL", "");
    vi.stubEnv("VITE_DEMO_MODE", "false");
    const { http, IS_DEMO } = await import("@/lib/api/http");
    const fetchMock = vi.fn(async (_target: string) => Response.json({ items: [] }));
    vi.stubGlobal("fetch", fetchMock);

    expect(IS_DEMO).toBe(false);
    await http("/api/v1/public/site", { anonymous: true });
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/api/v1/public/site");
  });

  it("uses serviceId for private availability and never sends appointment duration", async () => {
    const { tokenStore } = await import("@/lib/auth/token");
    const { realOperations } = await import("@/lib/api/operations");
    tokenStore.setSession({
      accessToken: "current",
      refreshToken: "refresh",
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    });
    const fetchMock = vi.fn(async (target: string, options: RequestInit) => {
      const url = new URL(target);
      if (url.pathname.endsWith("/availability")) {
        expect(url.searchParams.get("professionalId")).toBe("professional");
        expect(url.searchParams.get("serviceId")).toBe("service");
        expect(url.searchParams.get("date")).toBe("2026-11-23");
        expect(url.searchParams.has("durationMinutes")).toBe(false);
        return Response.json({ availableSlots: ["09:00:00"] });
      }
      expect(url.pathname).toBe("/api/v1/agenda/appointments");
      expect(JSON.parse(String(options.body))).toEqual({
        customerId: "customer",
        serviceId: "service",
        professionalId: "professional",
        date: "2026-11-23",
        time: "09:00:00",
      });
      return Response.json({ id: "appointment", durationMinutes: 45 }, { status: 201 });
    });
    vi.stubGlobal("fetch", fetchMock);

    await realOperations.availability("professional", "2026-11-23", "service");
    await realOperations.createAppointment({
      customerId: "customer",
      serviceId: "service",
      professionalId: "professional",
      date: "2026-11-23",
      time: "09:00:00",
    });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("renews an expired session only once for concurrent requests", async () => {
    const { tokenStore } = await import("@/lib/auth/token");
    const { http } = await import("@/lib/api/http");
    tokenStore.setSession({
      accessToken: "expired",
      refreshToken: "refresh-one",
      expiresAt: new Date(0).toISOString(),
    });
    const refresh = {
      accessToken: "valid",
      refreshToken: "refresh-two",
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    };
    const fetchMock = vi.fn(async (target: string, options: RequestInit) => {
      const headers = new Headers(options.headers);
      expect(headers.has("X-Tenant-Id")).toBe(false);
      if (target.endsWith("/api/auth/refresh")) {
        expect(headers.has("Authorization")).toBe(false);
        expect(JSON.parse(String(options.body))).toEqual({ refreshToken: "refresh-one" });
        return Response.json(refresh);
      }
      expect(headers.get("Authorization")).toBe("Bearer valid");
      return Response.json({ items: [] });
    });
    vi.stubGlobal("fetch", fetchMock);

    await Promise.all([http("/api/v1/customers"), http("/api/v1/catalog/services")]);

    expect(fetchMock.mock.calls.filter(([url]) => url.endsWith("/api/auth/refresh"))).toHaveLength(
      1,
    );
    expect(tokenStore.session()).toMatchObject(refresh);
  });

  it("keeps public requests anonymous and maps a conflict response", async () => {
    const { tokenStore } = await import("@/lib/auth/token");
    const { http } = await import("@/lib/api/http");
    tokenStore.setSession({
      accessToken: "current",
      refreshToken: "refresh",
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    });
    vi.stubGlobal(
      "fetch",
      vi.fn(async (target: string, options: RequestInit) => {
        expect(target).toBe(
          "https://api.example.test/api/v1/public/site/appointments?host=demo.example.test",
        );
        expect(new Headers(options.headers).has("Authorization")).toBe(false);
        return Response.json({ message: "Horário ocupado." }, { status: 409 });
      }),
    );
    await expect(
      http("/api/v1/public/site/appointments?host=demo.example.test", {
        method: "POST",
        anonymous: true,
      }),
    ).rejects.toMatchObject({
      status: 409,
      message: "Horário ocupado.",
    });
    expect(tokenStore.get()).toBe("current");
  });

  it("uses the public site routes and leaves booking duration to the server", async () => {
    const { realPublicSite } = await import("@/lib/api/public-site");
    const host = "estudio.example.test";
    const selection = { serviceId: "service", professionalId: "person", date: "2026-11-23" };
    const booking = { ...selection, name: "Cliente", phone: "11987654321", time: "09:00:00" };
    const routes: string[] = [];
    vi.stubGlobal(
      "fetch",
      vi.fn(async (target: string, options: RequestInit) => {
        const url = new URL(target);
        const route = url.pathname.replace("/api/v1/public/site", "");
        routes.push(route);
        expect(url.searchParams.get("host")).toBe(host);
        expect(new Headers(options.headers).has("Authorization")).toBe(false);
        expect(new Headers(options.headers).has("X-Tenant-Id")).toBe(false);
        if (route === "/availability") {
          for (const [key, value] of Object.entries(selection))
            expect(url.searchParams.get(key)).toBe(value);
          return Response.json({ slots: [booking.time] });
        }
        if (route === "/appointments") {
          expect(options.method).toBe("POST");
          expect(JSON.parse(String(options.body))).toEqual(booking);
          return Response.json({
            id: "reservation",
            date: booking.date,
            time: booking.time,
            durationMinutes: 45,
          });
        }
        return Response.json([]);
      }),
    );
    await realPublicSite.services(host);
    await realPublicSite.professionals(host);
    await expect(
      realPublicSite.availability(
        host,
        selection.serviceId,
        selection.professionalId,
        selection.date,
      ),
    ).resolves.toEqual({ slots: [booking.time] });
    await expect(realPublicSite.book(host, booking)).resolves.toMatchObject({
      id: "reservation",
      durationMinutes: 45,
    });
    expect(routes).toEqual(["/services", "/professionals", "/availability", "/appointments"]);
  });

  it("does not resend an old request if the user signs in to another tenant", async () => {
    const { tokenStore } = await import("@/lib/auth/token");
    const { http } = await import("@/lib/api/http");
    const expiresAt = new Date(Date.now() + 900_000).toISOString();
    tokenStore.setSession({ accessToken: "old", refreshToken: "old-refresh", expiresAt });
    const mock = vi.fn(async () => {
      tokenStore.setSession({
        accessToken: "other-tenant",
        refreshToken: "other-refresh",
        expiresAt,
      });
      return new Response(null, { status: 401 });
    });
    vi.stubGlobal("fetch", mock);

    await expect(http("/api/v1/customers", { method: "POST", json: {} })).rejects.toMatchObject({
      status: 401,
    });
    expect(mock).toHaveBeenCalledTimes(1);
    expect(tokenStore.get()).toBe("other-tenant");
  });
});
