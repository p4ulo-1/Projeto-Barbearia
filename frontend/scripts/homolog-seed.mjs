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
import { readFile } from "node:fs/promises";
import pg from "pg";

const EXPECTED_DATABASE = "servia_av2_homolog";
const EXPECTED_MARKER = "av2-fase1-isolada-v1";
const stackEnv = await readFile(new URL("../.homolog-artifacts/stack.env", import.meta.url), "utf8");
const dbPassword = stackEnv.match(/^SERVIA_HOMOLOG_DB_PASSWORD=([a-f0-9]+)$/m)?.[1];
if (!dbPassword) throw new Error("Execute npm run homolog:stack antes do seed.");
const DB_URL = `postgres://servia_homolog:${dbPassword}@127.0.0.1:15433/servia_av2_homolog`;

const base = process.env["SERVIA_SMOKE_BASE_URL"];
if (!base) {
  throw new Error("Defina SERVIA_SMOKE_BASE_URL com a origem local do Vite.");
}

const origin = new URL(base);
if (!["localhost", "127.0.0.1"].includes(origin.hostname)) {
  throw new Error("O seed só aceita um servidor local de desenvolvimento.");
}

// Dupla prova ANTES da primeira escrita: a API por trás do proxy e a conexão
// direta para preparar o perfil Professional apontam ao banco descartável.
const guard = await fetch(new URL("/api/__homolog/guard", origin));
if (!guard.ok) throw new Error(`API não confirma isolamento: HTTP ${guard.status}`);
const destination = await guard.json();
if (destination.database !== EXPECTED_DATABASE || destination.marker !== EXPECTED_MARKER)
  throw new Error("A API não está conectada à stack isolada de homologação.");
const db = new pg.Client({ connectionString: DB_URL });
await db.connect();
const database = await db.query("select current_database() as name");
if (database.rows[0]?.name !== EXPECTED_DATABASE) {
  await db.end();
  throw new Error("Conexão PostgreSQL fora do banco isolado de homologação.");
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

// Segundo profissional é usado somente para provar o escopo da agenda
// própria. Não o tornamos elegível nem consumimos outra vaga do plano.
const otherProfessional = await request("/api/v1/team/professionals", {
  method: "POST", token, expected: 201,
  body: {
    name: "Outro Profissional Homolog",
    workDays: [1, 2, 3, 4, 5, 6],
    startTime: "09:00:00", endTime: "18:00:00", slotIntervalMinutes: 30,
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

// 7. Site publicado com o widget real de agendamento.
await request("/api/v1/site", {
  method: "POST",
  token,
  expected: 201,
  body: { subdomain: slug },
});

const pages = await db.query(`select p."Id" from "SitePages" p
  join "Sites" s on s."Id" = p."SiteId"
  where s."TenantId" = $1 and p."Slug" = ''`, [signup.tenantId]);
const pageId = pages.rows[0]?.Id;
if (!pageId) throw new Error("Página inicial do site não encontrada no banco isolado.");
await request(`/api/v1/site/pages/${pageId}/sections`, {
  method: "POST", token, expected: 201,
  body: { componentType: 8, configuration: { title: "Reserve seu horário" } },
});
await request("/api/v1/site/publish", { method: "POST", token });

// A API ainda não expõe cadastro/vínculo de usuário Professional (AV2-06).
// Só neste banco descartável, reutilizamos o hash da senha aleatória do Owner
// e vinculamos uma conta Professional ao registro já criado pela API.
const professionalEmail = `prof-${suffix}@example.invalid`;
const professionalUserId = randomUUID();
await db.query("begin");
try {
  const inserted = await db.query(`
    insert into "Users" ("Id", "TenantId", "Name", "Email", "PasswordHash", "Role", "IsActive", "CreatedAt")
    select $1, "TenantId", 'Profissional Homolog', $2, "PasswordHash", 'Professional', true, now()
    from "Users" where "TenantId" = $3 and "Email" = $4
    returning "Id"`, [professionalUserId, professionalEmail, signup.tenantId, email]);
  if (inserted.rowCount !== 1) throw new Error("Owner do seed não encontrado no banco isolado.");
  const linked = await db.query(`update "Professionals" set "UserId" = $1
    where "TenantId" = $2 and "Id" = $3`,
  [professionalUserId, signup.tenantId, professional.id]);
  if (linked.rowCount !== 1) throw new Error("Profissional do seed não encontrado.");
  await db.query("commit");
} catch (error) {
  await db.query("rollback");
  throw error;
}

// Data futura em dia útil (segunda-feira seguinte).
const today = new Date();
const daysAhead = ((8 - today.getDay()) % 7) || 7;
const date = new Date(today.getTime() + daysAhead * 86_400_000)
  .toISOString()
  .slice(0, 10);

// Compromisso passado apenas no banco descartável: a API corretamente não
// permite criar novas reservas no passado, mas a tela deve permitir conclusão.
const past = new Date(today.getTime() - (((today.getDay() + 6) % 7) + 7) * 86_400_000)
  .toISOString().slice(0, 10);
const pastAppointmentId = randomUUID();
await db.query(`insert into "Appointments"
  ("Id", "TenantId", "CustomerId", "ServiceId", "ProfessionalId", "Date", "Time", "DurationMinutes", "Status", "CreatedAt")
  values ($1, $2, $3, $4, $5, $6, '09:00:00', 30, 'Confirmed', now())`,
[pastAppointmentId, signup.tenantId, customer.id, service.id, professional.id, past]);

// Registro sintético de outro profissional: serve exclusivamente ao teste de
// autorização, não à elegibilidade. Existe somente no PostgreSQL descartável.
const otherAppointmentId = randomUUID();
await db.query(`insert into "Appointments"
  ("Id", "TenantId", "CustomerId", "ServiceId", "ProfessionalId", "Date", "Time", "DurationMinutes", "Status", "CreatedAt")
  values ($1, $2, $3, $4, $5, $6, '16:00:00', 30, 'Confirmed', now())`,
[otherAppointmentId, signup.tenantId, customer.id, service.id, otherProfessional.id, date]);
await db.end();

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
      pastDate: past,
      pastAppointmentId,
      professionalEmail,
      professionalPassword: password,
      professionalUserId,
      otherProfessionalId: otherProfessional.id,
      otherAppointmentId,
      database: EXPECTED_DATABASE,
      guardMarker: EXPECTED_MARKER,
    },
    null,
    2,
  ),
);
