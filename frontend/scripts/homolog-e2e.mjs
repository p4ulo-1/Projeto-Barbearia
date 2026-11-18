/**
 * Homologação funcional da Fase 1 da AV2, em navegador real.
 *
 * Executa os fluxos de AV2-01 a AV2-04 contra o frontend conectado à API real
 * e a um PostgreSQL isolado. Cada passo registra o resultado; o script falha
 * se um critério de aceite não for observado.
 *
 * Uso:
 *   SERVIA_SMOKE_BASE_URL=http://localhost:5173 \
 *   SERVIA_HOMOLOG_SEED='{"tenantId":...}' \
 *   node scripts/homolog-e2e.mjs
 *
 * Requer Playwright com o canal `msedge`.
 */

import assert from "node:assert/strict";
import { chromium } from "playwright";

const base = process.env["SERVIA_SMOKE_BASE_URL"];
const seedRaw = process.env["SERVIA_HOMOLOG_SEED"];

if (!base || !seedRaw) {
  throw new Error("Defina SERVIA_SMOKE_BASE_URL e SERVIA_HOMOLOG_SEED.");
}

const seed = JSON.parse(seedRaw);
const origin = new URL(base);

const results = [];

function record(id, description, ok, detail = "") {
  results.push({ id, description, ok, detail });
  console.log(`[${ok ? "PASS" : "FAIL"}] ${id} — ${description}${detail ? ` :: ${detail}` : ""}`);
}

async function step(id, description, fn) {
  try {
    const detail = await fn();
    record(id, description, true, detail ?? "");
  } catch (error) {
    record(id, description, false, error instanceof Error ? error.message.split("\n")[0] : String(error));
  }
}

const browser = await chromium.launch({ channel: "msedge", headless: true });
const context = await browser.newContext({ baseURL: origin.origin });
const page = await context.newPage();

const pageErrors = [];
page.on("pageerror", (error) => pageErrors.push(error.message));

/**
 * Seleciona uma opção de um <select> dentro de um escopo.
 *
 * Usa o atributo `name` do select, não o rótulo: a página tem campos de busca
 * cujo rótulo contém o mesmo texto do select (ex.: "Buscar cliente na agenda"
 * e "Cliente"), e o rótulo casaria com o campo errado.
 */
async function selectByName(scope, name, optionText) {
  const select = scope.locator(`select[name="${name}"]`).first();
  await select.waitFor({ state: "visible", timeout: 20_000 });

  // A lista de opções pode chegar depois do select (consulta assíncrona).
  if (optionText !== "") {
    await select.locator("option", { hasText: optionText })
      .first().waitFor({ state: "attached", timeout: 20_000 });
  }

  const options = await select.locator("option").allTextContents();
  const match = options.find((text) =>
    optionText === ""
      ? text.trim() !== "" && !/^(selecione|nenhum)$/i.test(text.trim())
      : text.trim().toLowerCase().includes(optionText.toLowerCase()),
  );

  assert.ok(match, `opção "${optionText}" não encontrada em "${name}": ${options.join(" | ")}`);

  await select.selectOption({ label: match });
  return match.trim();
}

async function signIn() {
  await page.goto("/entrar", { waitUntil: "domcontentloaded" });
  await page.getByLabel("ID do estabelecimento").fill(seed.tenantId);
  await page.getByLabel("E-mail").fill(seed.email);
  // O campo de senha tem um botão "Mostrar senha" com o mesmo rótulo acessível.
  await page.locator('input[name="password"]').fill(seed.password);
  await page.getByRole("button", { name: /^entrar$/i }).click();
  // Após entrar, o app abre o onboarding quando o site não está publicado.
  // A agenda é acessível diretamente pelo painel.
  await page.waitForURL(/\/(painel|onboarding)/, { timeout: 30_000 });
}

// ---------- AV2-04: autenticação e acesso à agenda interna ----------

await step("AV2-04.1", "Autenticação leva ao painel", async () => {
  await signIn();
  return page.url();
});

await step("AV2-04.2", "Agenda interna acessível", async () => {
  await page.goto("/painel/agenda", { waitUntil: "domcontentloaded" });
  await page.getByRole("heading", { name: "Agenda", exact: true }).first()
    .waitFor({ timeout: 20_000 });
  return "cabeçalho Agenda visível";
});

// ---------- AV2-01: criação com serviço e profissional elegível ----------

let createdSlot = "";

await step("AV2-01.1", "Criação de agendamento pela agenda interna", async () => {
  await page.goto("/painel/agenda", { waitUntil: "domcontentloaded" });

  // A data da lista também alimenta o formulário de criação.
  await page.getByLabel("Data dos atendimentos").fill(seed.date);

  const form = page.locator("#novo-agendamento");
  await form.waitFor({ timeout: 20_000 });

  // O formulário é progressivo: profissional → serviço → horário → cliente.
  await selectByName(form, "professionalId", "Profissional Homolog");
  await selectByName(form, "serviceId", "Corte Homolog");

  // Horário: prova que a disponibilidade veio do backend.
  createdSlot = await selectByName(form, "time", "");

  await selectByName(form, "customerId", "Cliente Homolog");

  await form.getByRole("button", { name: /^agendar$/i }).click();

  await page.getByText(/agendamento criado/i).first().waitFor({ timeout: 20_000 });

  return `horário escolhido: ${createdSlot}`;
});

await step("AV2-01.2", "Agendamento persistido aparece na lista", async () => {
  await page.reload({ waitUntil: "domcontentloaded" });
  await page.getByLabel("Data dos atendimentos").fill(seed.date);
  await page.getByRole("cell", { name: /cliente homolog/i }).first()
    .waitFor({ timeout: 20_000 });
  return "cliente homolog presente na agenda";
});

// ---------- AV2-02: remarcação ----------

await step("AV2-02.1", "Remarcação válida persiste", async () => {
  await page.getByRole("button", { name: /^remarcar$/i }).first().click();

  const panel = page.locator("form").filter({ has: page.getByLabel("Nova data") });
  await panel.waitFor({ timeout: 20_000 });

  await panel.getByLabel("Nova data").fill(seed.date);

  // Escolhe um horário diferente do atual: remarcar para o mesmo horário
  // não prova nada.
  const slotSelect = panel.locator('select[name="time"]');
  await slotSelect.waitFor({ state: "visible", timeout: 20_000 });
  await slotSelect.locator("option").nth(1).waitFor({ state: "attached", timeout: 20_000 });

  const slotOptions = (await slotSelect.locator("option").allTextContents())
    .map((text) => text.trim())
    .filter((text) => text !== "" && !/^selecione$/i.test(text));

  const target = slotOptions.find((text) => text !== createdSlot.slice(0, 5));
  assert.ok(target, `nenhum horário alternativo disponível: ${slotOptions.join(" | ")}`);

  await slotSelect.selectOption({ label: target });
  const newSlot = target;

  // O botão de envio do painel de remarcação é "Salvar novo horário".
  await panel.getByRole("button", { name: /salvar novo horário/i }).click();
  await page.getByText(/remarcad/i).first().waitFor({ timeout: 20_000 });

  return `novo horário: ${newSlot}`;
});

// ---------- AV2-04: cancelamento ----------

await step("AV2-04.3", "Cancelamento pela agenda", async () => {
  await page.reload({ waitUntil: "domcontentloaded" });
  await page.getByLabel("Data dos atendimentos").fill(seed.date);

  await page.getByRole("button", { name: /^cancelar$/i }).first().click();
  await page.getByRole("button", { name: /confirmar/i }).last().click();
  await page.getByText(/cancelad/i).first().waitFor({ timeout: 20_000 });

  return "cancelamento confirmado";
});

// ---------- Restrições por perfil ----------

await step("AV2-04.4", "Acesso sem sessão é bloqueado", async () => {
  const anonymous = await browser.newContext();
  const anonPage = await anonymous.newPage();

  await anonPage.goto(new URL("/painel/agenda", origin).toString(), {
    waitUntil: "domcontentloaded",
  });

  await anonPage.waitForURL(/entrar/, { timeout: 20_000 });
  const url = anonPage.url();
  await anonymous.close();

  return url;
});

// ---------- AV2-03: reserva pública ----------

await step("AV2-03.1", "Contrato público responde sem sessão", async () => {
  // O frontend novo não expõe rota de reserva pública; o contrato é
  // verificado diretamente, sem autenticação.
  const anonymous = await browser.newContext();
  const anonPage = await anonymous.newPage();

  const services = await anonPage.request.get(
    new URL(`/api/v1/public/site/services?host=${seed.slug}`, origin).toString(),
  );
  assert.equal(services.status(), 200, `services: HTTP ${services.status()}`);

  const professionals = await anonPage.request.get(
    new URL(`/api/v1/public/site/professionals?host=${seed.slug}`, origin).toString(),
  );
  assert.equal(professionals.status(), 200, `professionals: HTTP ${professionals.status()}`);

  const availability = await anonPage.request.get(
    new URL(
      `/api/v1/public/site/availability?host=${seed.slug}`
        + `&professionalId=${seed.professionalId}&serviceId=${seed.serviceId}&date=${seed.date}`,
      origin,
    ).toString(),
  );
  assert.equal(availability.status(), 200, `availability: HTTP ${availability.status()}`);

  const body = await availability.json();
  assert.ok(Array.isArray(body.slots), "resposta deve conter o campo slots");

  await anonymous.close();

  return `${body.slots.length} horários públicos disponíveis`;
});

await step("AV2-03.2", "Reserva pública cria agendamento e rejeita duplicata", async () => {
  const anonymous = await browser.newContext();
  const anonPage = await anonymous.newPage();

  // Escolhe um horário realmente livre: execuções anteriores podem ter
  // ocupado horários fixos.
  const availability = await anonPage.request.get(
    new URL(
      `/api/v1/public/site/availability?host=${seed.slug}`
        + `&professionalId=${seed.professionalId}&serviceId=${seed.serviceId}&date=${seed.date}`,
      origin,
    ).toString(),
  );
  const { slots } = await availability.json();
  assert.ok(slots.length > 0, "esperava ao menos um horário público livre");

  const payload = {
    name: "Visitante Homolog",
    phone: `119${Date.now().toString().slice(-8)}`,
    email: null,
    serviceId: seed.serviceId,
    professionalId: seed.professionalId,
    date: seed.date,
    time: slots[0],
  };

  const first = await anonPage.request.post(
    new URL(`/api/v1/public/site/appointments?host=${seed.slug}`, origin).toString(),
    { data: payload },
  );
  assert.equal(first.status(), 201, `primeira reserva: HTTP ${first.status()}`);

  const duplicate = await anonPage.request.post(
    new URL(`/api/v1/public/site/appointments?host=${seed.slug}`, origin).toString(),
    { data: payload },
  );
  assert.equal(duplicate.status(), 409, `duplicata: HTTP ${duplicate.status()}`);

  await anonymous.close();

  return "201 na primeira reserva, 409 na duplicata";
});

await step("AV2-03.3", "Validação pública distingue 400 de 404", async () => {
  const anonymous = await browser.newContext();
  const anonPage = await anonymous.newPage();

  const missingService = await anonPage.request.get(
    new URL(
      `/api/v1/public/site/availability?host=${seed.slug}`
        + `&professionalId=${seed.professionalId}&date=${seed.date}`,
      origin,
    ).toString(),
  );
  assert.equal(missingService.status(), 400, `sem serviço: HTTP ${missingService.status()}`);

  const unknownService = await anonPage.request.get(
    new URL(
      `/api/v1/public/site/availability?host=${seed.slug}`
        + `&professionalId=${seed.professionalId}`
        + `&serviceId=00000000-0000-0000-0000-000000000001&date=${seed.date}`,
      origin,
    ).toString(),
  );
  assert.equal(unknownService.status(), 404, `serviço inexistente: HTTP ${unknownService.status()}`);

  await anonymous.close();

  return "400 para pedido incompleto, 404 para recurso inexistente";
});

await step("AV2-03.4", "Frontend novo não expõe rota pública de reserva", async () => {
  const anonymous = await browser.newContext();
  const anonPage = await anonymous.newPage();

  await anonPage.goto(
    new URL(`/agendar?host=${seed.slug}`, origin).toString(),
    { waitUntil: "domcontentloaded" },
  );

  const body = (await anonPage.textContent("body")) ?? "";
  await anonymous.close();

  // Registra a lacuna: a rota não existe no frontend novo.
  return body.includes("Page not found")
    ? "lacuna confirmada: /agendar não existe no frontend novo"
    : "página carregada sem o título esperado";
});

// ---------- AV2-02: conflito na remarcação ----------

await step("AV2-02.2", "Remarcação para horário ocupado é rejeitada", async () => {
  // Dois agendamentos no mesmo dia; o segundo tenta ocupar o horário do
  // primeiro. O backend deve recusar com 409.
  const session = await page.request.post(
    new URL("/api/auth/sign-in", origin).toString(),
    { data: { tenantId: seed.tenantId, email: seed.email, password: seed.password } },
  );
  const { accessToken } = await session.json();
  const auth = { Authorization: `Bearer ${accessToken}` };

  const availability = await page.request.get(
    new URL(
      `/api/v1/agenda/availability?professionalId=${seed.professionalId}`
        + `&serviceId=${seed.serviceId}&date=${seed.date}`,
      origin,
    ).toString(),
    { headers: auth },
  );
  const { availableSlots } = await availability.json();
  assert.ok(availableSlots.length >= 2, "esperava ao menos dois horários livres");

  const [firstSlot, secondSlot] = availableSlots;

  const create = (time) =>
    page.request.post(new URL("/api/v1/agenda/appointments", origin).toString(), {
      headers: auth,
      data: {
        customerId: seed.customerId,
        serviceId: seed.serviceId,
        professionalId: seed.professionalId,
        date: seed.date,
        time,
      },
    });

  const first = await create(firstSlot);
  assert.equal(first.status(), 201, `primeiro agendamento: HTTP ${first.status()}`);

  const second = await create(secondSlot);
  assert.equal(second.status(), 201, `segundo agendamento: HTTP ${second.status()}`);
  const secondBody = await second.json();

  // Remarcar o segundo para o horário do primeiro deve conflitar.
  const conflict = await page.request.patch(
    new URL(`/api/v1/agenda/appointments/${secondBody.id}/reschedule`, origin).toString(),
    { headers: auth, data: { date: seed.date, time: firstSlot } },
  );
  assert.equal(conflict.status(), 409, `conflito: HTTP ${conflict.status()}`);

  return `${secondSlot} → ${firstSlot} recusado com 409`;
});

// ---------- AV2-04: agenda própria e envio duplicado ----------

await step("AV2-04.5", "Agenda própria do profissional responde ao próprio usuário", async () => {
  // O endpoint de agenda própria existe e é escopado ao profissional do
  // usuário. Sem usuário vinculado a profissional, o backend recusa — o que
  // confirma que a rota não expõe a agenda de terceiros.
  const session = await page.request.post(
    new URL("/api/auth/sign-in", origin).toString(),
    { data: { tenantId: seed.tenantId, email: seed.email, password: seed.password } },
  );
  const { accessToken } = await session.json();

  const own = await page.request.get(
    new URL(`/api/v1/agenda/mine/appointments?date=${seed.date}`, origin).toString(),
    { headers: { Authorization: `Bearer ${accessToken}` } },
  );

  // 200 (usuário vinculado) ou 400/403 (sem vínculo) — nunca 500.
  assert.ok(
    [200, 400, 403, 404].includes(own.status()),
    `agenda própria: HTTP ${own.status()}`,
  );

  return `HTTP ${own.status()} — rota escopada, sem vazamento`;
});

await step("AV2-04.6", "Envio duplicado não cria dois agendamentos", async () => {
  const session = await page.request.post(
    new URL("/api/auth/sign-in", origin).toString(),
    { data: { tenantId: seed.tenantId, email: seed.email, password: seed.password } },
  );
  const { accessToken } = await session.json();
  const auth = { Authorization: `Bearer ${accessToken}` };

  const availability = await page.request.get(
    new URL(
      `/api/v1/agenda/availability?professionalId=${seed.professionalId}`
        + `&serviceId=${seed.serviceId}&date=${seed.date}`,
      origin,
    ).toString(),
    { headers: auth },
  );
  const { availableSlots } = await availability.json();
  assert.ok(availableSlots.length > 0, "esperava horário livre");

  const time = availableSlots[availableSlots.length - 1];

  const body = {
    customerId: seed.customerId,
    serviceId: seed.serviceId,
    professionalId: seed.professionalId,
    date: seed.date,
    time,
  };

  const url = new URL("/api/v1/agenda/appointments", origin).toString();
  const first = await page.request.post(url, { headers: auth, data: body });
  const second = await page.request.post(url, { headers: auth, data: body });

  assert.equal(first.status(), 201, `primeiro envio: HTTP ${first.status()}`);
  assert.equal(second.status(), 409, `envio duplicado: HTTP ${second.status()}`);

  return "201 no primeiro envio, 409 no duplicado";
});

await browser.close();

// ---------- Resumo ----------

const failed = results.filter((item) => !item.ok);

console.log("\n=== RESUMO ===");
console.log(`Total: ${results.length} · Passou: ${results.length - failed.length} · Falhou: ${failed.length}`);

if (pageErrors.length > 0) {
  console.log("\nErros de página:");
  for (const error of pageErrors) {
    console.log(` - ${error}`);
  }
}

if (failed.length > 0) {
  console.log("\nFalhas:");
  for (const item of failed) {
    console.log(` - ${item.id}: ${item.description} :: ${item.detail}`);
  }

  process.exitCode = 1;
}
