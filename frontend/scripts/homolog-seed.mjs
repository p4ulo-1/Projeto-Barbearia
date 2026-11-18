/**
 * Semeia um estabelecimento isolado para a homologação da Fase 1.
 *
 * Cria tenant, proprietário, serviço, profissional vinculado e cliente pela
 * API real. Não usa dados simulados: tudo é persistido no PostgreSQL do
 * ambiente de homologação.
 *
 * Uso:
 *   SERVIA_SMOKE_BASE_URL=http://localhost:5173 node scripts/homolog-seed.mjs
 *
 * Imprime um JSON com os identificadores e credenciais para o teste de
 * navegador consumir.
 */

import { randomUUID } from "node:crypto";

const base = process.env["SERVIA_SMOKE_BASE_URL"];
if (!base) {
  throw new Error("Defina SERVIA_SMOKE_BASE_URL com a origem local do Vite.");
}

const origin = new URL(base);
if (!["localhost", "127.0.0.1"].includes(origin.hostname)) {
  throw new Error("O seed só aceita um servidor local de desenvolvimento.");
}

async function request(path, { method = "GET", body, token, expected = 200 } = {}) {
  const response = await fetch(new URL(path, origin), {
    method,
    headers: {
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    cache: "no-store",
  });

  const text = await response.text();

  if (response.status !== expected) {
    throw new Error(`${method} ${path}: esperado ${expected}, obtido ${response.status} — ${text}`);
  }

  return text === "" ? undefined : JSON.parse(text);
}

const suffix = randomUUID().replaceAll("-", "").slice(0, 10);
const email = `homolog-${suffix}@example.invalid`;
const password = `Homolog!${randomUUID().slice(0, 12)}`;
const slug = `homolog-${suffix}`;

// 1. Provisiona o estabelecimento e o proprietário.
const signup = await request("/api/v1/public/signup", {
  method: "POST",
  expected: 201,
  body: {
    slug,
    name: `Estabelecimento Homolog ${suffix}`,
    segment: 1,
    document: `HOMOLOG-${suffix}`,
    ownerName: "Proprietário Homolog",
    ownerEmail: email,
    ownerPassword: password,
  },
});

// 2. Autentica.
const session = await request("/api/auth/sign-in", {
  method: "POST",
  body: { tenantId: signup.tenantId, email, password },
});

const token = session.accessToken;

// 3. Serviço de 30 minutos.
const service = await request("/api/v1/catalog/services", {
  method: "POST",
  token,
  expected: 201,
  body: { name: "Corte Homolog", price: 50, durationMinutes: 30 },
});

// 4. Profissional com jornada de segunda a sábado, 09:00–18:00.
const professional = await request("/api/v1/team/professionals", {
  method: "POST",
  token,
  expected: 201,
  body: {
    name: "Profissional Homolog",
    workDays: [1, 2, 3, 4, 5, 6],
    startTime: "09:00:00",
    endTime: "18:00:00",
    slotIntervalMinutes: 30,
  },
});

// 5. Vínculo profissional-serviço: sem ele não há agendamento.
await request(
  `/api/v1/team/professionals/${professional.id}/services/${service.id}`,
  { method: "POST", token, expected: 204 },
);

// 6. Cliente.
const customer = await request("/api/v1/customers", {
  method: "POST",
  token,
  expected: 201,
  body: { name: "Cliente Homolog", phone: `119${suffix.slice(0, 8)}` },
});

// 7. Site publicado: os endpoints públicos só respondem com site publicado.
//    Sem isso, a reserva pública retorna 404 — comportamento correto, mas
//    impede exercitar o fluxo.
await request("/api/v1/site", {
  method: "POST",
  token,
  expected: 201,
  body: { subdomain: slug },
});

await request("/api/v1/site/publish", { method: "POST", token });

// Data futura em dia útil (segunda-feira seguinte).
const today = new Date();
const daysAhead = ((8 - today.getDay()) % 7) || 7;
const date = new Date(today.getTime() + daysAhead * 86_400_000)
  .toISOString()
  .slice(0, 10);

console.log(
  JSON.stringify(
    {
      tenantId: signup.tenantId,
      slug,
      email,
      password,
      serviceId: service.id,
      professionalId: professional.id,
      customerId: customer.id,
      date,
    },
    null,
    2,
  ),
);
