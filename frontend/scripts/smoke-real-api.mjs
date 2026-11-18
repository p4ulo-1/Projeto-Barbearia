import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";

// Run only against an isolated development API: this creates a test tenant and customer.
const base = process.env["SERVIA_SMOKE_BASE_URL"];
if (!base) {
  throw new Error("Defina SERVIA_SMOKE_BASE_URL com a origem local do Vite.");
}
const origin = new URL(base);
if (!["localhost", "127.0.0.1"].includes(origin.hostname)) {
  throw new Error("O smoke só aceita um servidor local de desenvolvimento.");
}

async function request(path, { method = "GET", body, token, status = 200 } = {}) {
  const response = await fetch(new URL(path, origin), {
    method,
    headers: {
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    cache: "no-store",
  });
  assert.equal(response.status, status, `${method} ${path}: HTTP ${response.status}`);
  return response.status === 204 ? undefined : response.json();
}

const suffix = randomUUID().replaceAll("-", "").slice(0, 12);
const email = `marcoa-${suffix}@example.invalid`;
const password = `MarcoA!${randomUUID().slice(0, 12)}`;
const signup = await request("/api/v1/public/signup", {
  method: "POST",
  status: 201,
  body: {
    slug: `marcoa-${suffix}`,
    name: "Estabelecimento de teste Marco A",
    segment: 1,
    document: "TESTE-MARCO-A",
    ownerName: "Proprietario de teste",
    ownerEmail: email,
    ownerPassword: password,
  },
});
assert.ok(signup.tenantId);

const session = await request("/api/auth/sign-in", {
  method: "POST",
  body: { tenantId: signup.tenantId, email, password },
});
assert.ok(session.accessToken && session.refreshToken && session.expiresAt);
const claims = JSON.parse(Buffer.from(session.accessToken.split(".")[1], "base64url").toString());
assert.equal(claims.tenant_id, signup.tenantId);
assert.equal(claims.email, email);
assert.equal(claims.role, "Owner");

const renewed = await request("/api/auth/refresh", {
  method: "POST",
  body: { refreshToken: session.refreshToken },
});
assert.ok(renewed.accessToken && renewed.refreshToken);
assert.notEqual(renewed.refreshToken, session.refreshToken);

const customer = await request("/api/v1/customers", {
  method: "POST",
  status: 201,
  token: renewed.accessToken,
  body: {
    name: "Cliente de teste Marco A",
    phone: "00000000000",
    email: `cliente-${suffix}@example.invalid`,
  },
});
assert.ok(customer.id);
const persisted = await request(`/api/v1/customers/${customer.id}`, {
  token: renewed.accessToken,
});
assert.equal(persisted.id, customer.id);
assert.equal(persisted.name, customer.name);

const service = await request("/api/v1/catalog/services", {
  method: "POST",
  status: 201,
  token: renewed.accessToken,
  body: { name: "Servico de teste Marco A", price: 10, durationMinutes: 30 },
});
const professional = await request("/api/v1/team/professionals", {
  method: "POST",
  status: 201,
  token: renewed.accessToken,
  body: {
    name: "Profissional de teste Marco A",
    workDays: [1, 2, 3, 4, 5],
    startTime: "08:00:00",
    endTime: "18:00:00",
    slotIntervalMinutes: 30,
  },
});
await request(`/api/v1/team/professionals/${professional.id}/services/${service.id}`, {
  method: "POST",
  status: 204,
  token: renewed.accessToken,
});
const nextMonday = new Date();
nextMonday.setUTCDate(nextMonday.getUTCDate() + ((8 - nextMonday.getUTCDay()) % 7 || 7));
const date = nextMonday.toISOString().slice(0, 10);
const availability = await request(
  `/api/v1/agenda/availability?${new URLSearchParams({
    professionalId: professional.id,
    serviceId: service.id,
    date,
  })}`,
  { token: renewed.accessToken },
);
assert.ok(Array.isArray(availability.availableSlots));
assert.ok(availability.availableSlots.length > 0);
const selectedTime = availability.availableSlots[0];
const appointment = await request("/api/v1/agenda/appointments", {
  method: "POST",
  status: 201,
  token: renewed.accessToken,
  body: { customerId: customer.id, serviceId: service.id, professionalId: professional.id, date, time: selectedTime },
});
assert.equal(appointment.durationMinutes, service.durationMinutes);
const rescheduleSlots = await request(
  `/api/v1/agenda/appointments/${appointment.id}/reschedule-availability?date=${date}`,
  { token: renewed.accessToken },
);
assert.ok(rescheduleSlots.availableSlots.includes(selectedTime));
const nextTime = rescheduleSlots.availableSlots.find((slot) => slot !== selectedTime);
assert.ok(nextTime);
const moved = await request(`/api/v1/agenda/appointments/${appointment.id}/reschedule`, {
  method: "PATCH", token: renewed.accessToken, body: { date, time: nextTime },
});
assert.equal(moved.durationMinutes, service.durationMinutes);
assert.equal(moved.time, nextTime);
const appointments = await request(`/api/v1/agenda/appointments?date=${date}`, {
  token: renewed.accessToken,
});
assert.ok(appointments.some((item) => item.id === appointment.id && item.time === nextTime));
await request("/api/v1/agenda/appointments", {
  method: "POST", status: 409, token: renewed.accessToken,
  body: { customerId: customer.id, serviceId: service.id, professionalId: professional.id, date, time: nextTime },
});
const cancelled = await request(`/api/v1/agenda/appointments/${appointment.id}/cancel`, {
  method: "POST", token: renewed.accessToken, body: { reason: "Smoke AV2" },
});
assert.equal(cancelled.status, "Cancelled");

await request("/api/auth/sign-out", {
  method: "POST",
  status: 204,
  body: { refreshToken: renewed.refreshToken },
});
await request("/api/auth/refresh", {
  method: "POST",
  status: 401,
  body: { refreshToken: renewed.refreshToken },
});

console.log(
  `Marco A/AV2 Fase 1: API real, sessão, disponibilidade, criação, remarcação, conflito, cancelamento e persistência OK (tenant ${signup.tenantId}).`,
);
