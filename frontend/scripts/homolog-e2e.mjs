/** Homologação AV2-01..04 em Chromium, API real e PostgreSQL isolado.
 * Veja frontend/README.md. Passos UI usam DOM; API confirma estado persistido.
 */
import assert from "node:assert/strict";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const base = process.env.SERVIA_SMOKE_BASE_URL;
const seedFile = process.env.SERVIA_HOMOLOG_SEED_FILE;
if (!base || !seedFile) throw new Error("Defina SERVIA_SMOKE_BASE_URL e SERVIA_HOMOLOG_SEED_FILE.");
const seed = JSON.parse(await readFile(seedFile, "utf8"));
const origin = new URL(base);
assert.ok(["localhost", "127.0.0.1"].includes(origin.hostname), "Vite deve ser local");
const guard = await fetch(new URL("/api/__homolog/guard", origin));
assert.equal(guard.status, 200, "API não confirma isolamento");
assert.deepEqual(await guard.json(), { database: "servia_av2_homolog", marker: "av2-fase1-isolada-v1" });
assert.equal(seed.database, "servia_av2_homolog");
const artifacts = new URL("../.homolog-artifacts/", import.meta.url);
await mkdir(artifacts, { recursive: true });
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ baseURL: origin.origin });
await context.tracing.start({ screenshots: true, snapshots: true, sources: true });
const page = await context.newPage();
const visitor = await browser.newContext();
const publicPage = await visitor.newPage();
const professional = await browser.newContext({ baseURL: origin.origin });
const proPage = await professional.newPage();
const results = [];
const pageErrors = [];
for (const [name, p] of [["owner", page], ["public", publicPage], ["professional", proPage]])
  p.on("pageerror", (error) => pageErrors.push(name + ": " + error.message));
const url = (path) => new URL(path, origin).toString();
const publicUrl = `http://${seed.slug}.localhost:${origin.port}/`;
async function step(id, kind, description, test) {
  try {
    const detail = await test();
    results.push({ id, kind, description, ok: true, detail: detail ?? "" });
    console.log(`[PASS] ${id} [${kind}] ${description}`);
  } catch (error) {
    const detail = error instanceof Error ? error.message.split("\n")[0] : String(error);
    results.push({ id, kind, description, ok: false, detail });
    console.error(`[FAIL] ${id} [${kind}] ${description}: ${detail}`);
    if (process.env.SERVIA_HOMOLOG_DEBUG === "true") console.error(error);
  }
}
async function login(p, email, password) {
  await p.goto("/entrar");
  await p.getByLabel("ID do estabelecimento").fill(seed.tenantId);
  await p.getByLabel("E-mail").fill(email);
  await p.locator('input[name="password"]').fill(password);
  await p.getByRole("button", { name: /^entrar$/i }).click();
  await p.waitForURL(/\/(painel|onboarding)/, { timeout: 30000 });
}
async function choose(scope, name, text) {
  const select = scope.locator(`select[name="${name}"]`).first();
  await select.selectOption({ label: text }, { timeout: 20000 });
  const value = await select.inputValue();
  assert.ok(value);
  return value;
}
async function signInApi(email, password) {
  const r = await context.request.post(url("/api/auth/sign-in"), {
    data: { tenantId: seed.tenantId, email, password },
  });
  assert.equal(r.status(), 200);
  return (await r.json()).accessToken;
}
const ownerToken = await signInApi(seed.email, seed.password);
const auth = { Authorization: `Bearer ${ownerToken}` };
async function appointments(date = seed.date) {
  const r = await context.request.get(url(`/api/v1/agenda/appointments?date=${date}`), { headers: auth });
  assert.equal(r.status(), 200);
  return r.json();
}
function row(time, p = page) {
  return p.getByRole("row").filter({ hasText: "Cliente Homolog" })
    .filter({ hasText: time.slice(0, 5) }).first();
}
function itemAt(items, time) {
  return items.find((item) => item.customerId === seed.customerId &&
    item.professionalId === seed.professionalId && item.time.slice(0, 5) === time.slice(0, 5));
}
const rescheduleForm = () => page.locator("form").filter({ has: page.getByLabel("Nova data") });
const bookForm = () => publicPage.locator("form").filter({
  has: publicPage.getByRole("button", { name: /confirmar horário/i }),
});
let createdTime, movedTime, appointmentId, publicTime, publicAppointmentId;

await step("AV2-04.1", "UI", "Owner autentica e abre agenda", async () => {
  await login(page, seed.email, seed.password);
  await page.goto("/painel/agenda");
  await page.getByRole("heading", { name: "Agenda", exact: true }).first().waitFor();
});
await step("AV2-01.1", "UI+API", "Profissional elegível, serviço vinculado e horários", async () => {
  await page.getByLabel("Data dos atendimentos").fill(seed.date);
  const form = page.locator("#novo-agendamento");
  assert.equal(await choose(form, "professionalId", "Profissional Homolog"), seed.professionalId);
  assert.equal(await choose(form, "serviceId", "Corte Homolog"), seed.serviceId);
  const query = new URLSearchParams({ professionalId: seed.professionalId,
    serviceId: seed.serviceId, date: seed.date });
  const r = await context.request.get(url(`/api/v1/agenda/availability?${query}`), { headers: auth });
  assert.equal(r.status(), 200);
  const slots = (await r.json()).availableSlots;
  assert.ok(slots.length > 1);
  const timeSelect = form.locator('select[name="time"]');
  try {
  await timeSelect.locator("option").nth(1).waitFor({ state: "attached", timeout: 5000 });
  } catch {
    throw new Error(`Horários não renderizados: ${(await form.innerText()).slice(0, 500)}`);
  }
  createdTime = await timeSelect.locator("option").nth(1).getAttribute("value");
  assert.ok(createdTime && slots.includes(createdTime),
    `horário da interface não corresponde à API: UI=${createdTime}; API=${slots.join(",")}`);
  await timeSelect.selectOption(createdTime);
  await choose(form, "customerId", "Cliente Homolog");
});
await step("AV2-01.2", "UI+API", "Criação persiste após recarga e duração vem do serviço", async () => {
  const [r] = await Promise.all([
    page.waitForResponse((response) => response.url().includes("/api/v1/agenda/appointments")
      && response.request().method() === "POST"),
    page.locator("#novo-agendamento").getByRole("button", { name: /^agendar$/i }).click(),
  ]);
  assert.equal(r.status(), 201);
  const created = await r.json();
  appointmentId = created.id;
  assert.equal(created.durationMinutes, 30);
  await page.reload();
  await page.getByLabel("Data dos atendimentos").fill(seed.date);
  await row(createdTime).waitFor();
  assert.match(await row(createdTime).innerText(), /30 min/);
  assert.equal(itemAt(await appointments(), createdTime)?.id, appointmentId);
});
await step("AV2-02.1", "UI+API", "Próprio horário é elegível e remarcação persiste", async () => {
  await row(createdTime).getByRole("button", { name: "Remarcar" }).click();
  const form = rescheduleForm();
  await form.getByLabel("Nova data").fill(seed.date);
  const select = form.locator('select[name="time"]');
  await select.locator(`option[value="${createdTime}"]`).waitFor({ state: "attached" });
  const values = await select.locator("option").evaluateAll((xs) => xs.map((x) => x.value).filter(Boolean));
  movedTime = values.find((x) => x !== createdTime);
  assert.ok(movedTime);
  await select.selectOption(movedTime);
  const response = page.waitForResponse((r) => r.url().includes(`/appointments/${appointmentId}/reschedule`)
    && r.request().method() === "PATCH");
  await form.getByRole("button", { name: /salvar novo horário/i }).click();
  assert.equal((await response).status(), 200);
  await page.reload();
  await page.getByLabel("Data dos atendimentos").fill(seed.date);
  await row(movedTime).waitFor();
  assert.equal(itemAt(await appointments(), movedTime)?.id, appointmentId);
});
await step("AV2-02.2", "UI+API", "Conflito concorrente é exibido e não altera agendamento", async () => {
  await row(movedTime).getByRole("button", { name: "Remarcar" }).click();
  const form = rescheduleForm();
  await form.getByLabel("Nova data").fill(seed.date);
  const select = form.locator('select[name="time"]');
  await select.locator("option").nth(1).waitFor({ state: "attached" });
  const values = await select.locator("option").evaluateAll((xs) => xs.map((x) => x.value).filter(Boolean));
  const target = values.find((x) => x !== movedTime);
  assert.ok(target);
  await select.selectOption(target);
  const blocker = await context.request.post(url("/api/v1/agenda/appointments"), {
    headers: auth, data: { customerId: seed.customerId, serviceId: seed.serviceId,
      professionalId: seed.professionalId, date: seed.date, time: target },
  });
  assert.equal(blocker.status(), 201);
  const response = page.waitForResponse((r) => r.url().includes(`/appointments/${appointmentId}/reschedule`)
    && r.request().method() === "PATCH");
  await form.getByRole("button", { name: /salvar novo horário/i }).click();
  assert.equal((await response).status(), 409);
  const toast = page.locator("[data-sonner-toast]").first();
  await toast.waitFor({ timeout: 5000 });
  const message = await toast.innerText();
  assert.match(message, /horário|conflito|ocupado|disponível/i,
    `mensagem inesperada de conflito: ${message}`);
  assert.equal(itemAt(await appointments(), movedTime)?.id, appointmentId);
});
await step("AV2-04.2", "UI+API", "Cancelamento persiste após recarga", async () => {
  await page.reload();
  await page.getByLabel("Data dos atendimentos").fill(seed.date);
  await row(movedTime).getByRole("button", { name: "Cancelar" }).click();
  const response = page.waitForResponse((r) => r.url().includes(`/appointments/${appointmentId}/cancel`)
    && r.request().method() === "POST");
  await page.getByRole("button", { name: /confirmar cancelamento/i }).click();
  assert.equal((await response).status(), 200);
  await page.reload();
  await page.getByLabel("Data dos atendimentos").fill(seed.date);
  assert.match(await row(movedTime).innerText(), /Cancelado/);
  assert.equal((await appointments()).find((x) => x.id === appointmentId)?.status, "Cancelled");
});
await step("AV2-04.3", "UI+API", "Atendimento passado é concluído e persiste", async () => {
  await page.getByLabel("Data dos atendimentos").fill(seed.pastDate);
  await row("09:00").waitFor();
  const response = page.waitForResponse((r) => r.url().includes(`/appointments/${seed.pastAppointmentId}/complete`)
    && r.request().method() === "POST");
  await row("09:00").getByRole("button", { name: "Concluir" }).click();
  assert.equal((await response).status(), 200);
  await page.reload();
  await page.getByLabel("Data dos atendimentos").fill(seed.pastDate);
  assert.match(await row("09:00").innerText(), /Concluído/);
  assert.equal((await appointments(seed.pastDate)).find((x) => x.id === seed.pastAppointmentId)?.status, "Completed");
});
await step("AV2-04.4", "UI", "Visitante não acessa agenda interna", async () => {
  const anonymous = await browser.newContext();
  const p = await anonymous.newPage();
  await p.goto(url("/painel/agenda"));
  await p.waitForURL(/entrar/);
  await anonymous.close();
});
await step("AV2-03.1", "UI", "Site publicado exibe widget com horários reais", async () => {
  await publicPage.goto(publicUrl);
  const form = bookForm();
  await form.waitFor();
  await form.getByLabel("Serviço").selectOption(seed.serviceId);
  await form.getByLabel("Profissional").selectOption(seed.professionalId);
  await form.getByLabel("Data").fill(seed.date);
  const select = form.getByLabel("Horário");
  await select.locator("option").nth(1).waitFor({ state: "attached" });
  publicTime = await select.locator("option").nth(1).getAttribute("value");
  assert.ok(publicTime);
  await select.selectOption(publicTime);
  await form.getByLabel("Nome").fill("Visitante Homolog UI");
  await form.getByLabel("Telefone").fill("11912345678");
  return publicUrl;
});
await step("AV2-03.2", "UI+API", "Reserva pública persiste e remove horário", async () => {
  const response = publicPage.waitForResponse((r) => r.url().includes("/api/v1/public/site/appointments")
    && r.request().method() === "POST");
  await bookForm().getByRole("button", { name: /confirmar horário/i }).click();
  const r = await response;
  assert.equal(r.status(), 201);
  const booked = await r.json();
  publicAppointmentId = booked.id;
  assert.equal(booked.durationMinutes, 30);
  await publicPage.getByText(/Agendamento confirmado/i).waitFor();
  assert.equal((await appointments()).find((x) => x.id === publicAppointmentId)?.time.slice(0, 5),
    publicTime.slice(0, 5));
  await publicPage.reload();
  const form = bookForm();
  await form.getByLabel("Serviço").selectOption(seed.serviceId);
  await form.getByLabel("Profissional").selectOption(seed.professionalId);
  await form.getByLabel("Data").fill(seed.date);
  await form.getByLabel("Horário").waitFor();
  assert.equal(await form.getByLabel("Horário").locator(`option[value="${publicTime}"]`).count(), 0);
});
await step("AV2-03.3", "UI+API", "Conflito público informa erro e limpa horário", async () => {
  const form = bookForm();
  const select = form.getByLabel("Horário");
  const time = await select.locator("option").nth(1).getAttribute("value");
  assert.ok(time);
  await select.selectOption(time);
  await form.getByLabel("Nome").fill("Conflito Homolog UI");
  await form.getByLabel("Telefone").fill("11912345679");
  const competing = await visitor.request.post(url(`/api/v1/public/site/appointments?host=${seed.slug}`), {
    data: { name: "Concorrente Homolog", phone: "11912345680", serviceId: seed.serviceId,
      professionalId: seed.professionalId, date: seed.date, time },
  });
  assert.equal(competing.status(), 201);
  const response = publicPage.waitForResponse((r) => r.url().includes("/api/v1/public/site/appointments")
    && r.request().method() === "POST");
  await form.getByRole("button", { name: /confirmar horário/i }).click();
  assert.equal((await response).status(), 409);
  await form.getByText(/horário deixou de estar disponível/i).waitFor();
  assert.equal(await select.inputValue(), "");
});
await step("AV2-03.4", "UI+API", "Envio duplo pela interface gera um único POST", async () => {
  const form = bookForm();
  const select = form.getByLabel("Horário");
  await select.locator("option").nth(1).waitFor({ state: "attached" });
  const time = await select.locator("option").nth(1).getAttribute("value");
  assert.ok(time);
  await select.selectOption(time);
  await form.getByLabel("Nome").fill("Duplicidade Homolog UI");
  await form.getByLabel("Telefone").fill("11912345681");
  await publicPage.waitForFunction(() => {
    const button = [...document.querySelectorAll("button")]
      .find((item) => item.textContent?.includes("Confirmar horário"));
    return button && !button.disabled;
  });
  let count = 0;
  const onRequest = (request) => {
    if (request.url().includes("/api/v1/public/site/appointments") && request.method() === "POST") count++;
  };
  publicPage.on("request", onRequest);
  await form.evaluate((element) => { element.requestSubmit(); element.requestSubmit(); });
  await publicPage.getByText(/Agendamento confirmado/i).waitFor();
  await publicPage.waitForTimeout(500);
  publicPage.off("request", onRequest);
  assert.equal(count, 1, "dois POSTs enviados pela interface");
  assert.equal((await appointments()).filter((x) => x.professionalId === seed.professionalId &&
    x.time.slice(0, 5) === time.slice(0, 5) && x.status === "Confirmed").length, 1);
});
await step("AV2-04.5", "UI+API", "Professional vê própria agenda e recebe 403 para equipe", async () => {
  await login(proPage, seed.professionalEmail, seed.professionalPassword);
  await proPage.goto("/painel/agenda");
  await proPage.getByText("Meus atendimentos").first().waitFor();
  assert.equal(await proPage.getByLabel("Filtrar profissional").count(), 0);
  await proPage.getByLabel("Data dos atendimentos").fill(seed.date);
  await proPage.getByRole("row").filter({ hasText: publicTime.slice(0, 5) }).first().waitFor();
  assert.equal(await proPage.getByRole("row").filter({ hasText: "16:00" }).count(), 0);
  const token = await signInApi(seed.professionalEmail, seed.professionalPassword);
  const headers = { Authorization: `Bearer ${token}` };
  const own = await context.request.get(url(`/api/v1/agenda/mine/appointments?date=${seed.date}`), { headers });
  assert.equal(own.status(), 200);
  const ownItems = await own.json();
  assert.ok(ownItems.some((x) => x.id === publicAppointmentId));
  assert.ok(!ownItems.some((x) => x.id === seed.otherAppointmentId));
  const team = await context.request.get(url(`/api/v1/agenda/appointments?date=${seed.date}`), { headers });
  assert.equal(team.status(), 403);
  const otherCancel = await context.request.post(
    url(`/api/v1/agenda/mine/appointments/${seed.otherAppointmentId}/cancel`),
    { headers, data: {} });
  assert.equal(otherCancel.status(), 403);
});
await step("AV2-04.6", "API", "Contrato público distingue 400 de 404", async () => {
  const missing = await visitor.request.get(url(`/api/v1/public/site/availability?host=${seed.slug}`
    + `&professionalId=${seed.professionalId}&date=${seed.date}`));
  assert.equal(missing.status(), 400);
  const unknown = await visitor.request.get(url(`/api/v1/public/site/availability?host=${seed.slug}`
    + `&professionalId=${seed.professionalId}&serviceId=00000000-0000-0000-0000-000000000001&date=${seed.date}`));
  assert.equal(unknown.status(), 404);
});
if (pageErrors.length) results.push({ id: "BROWSER", kind: "UI",
  description: "Erros não tratados no navegador", ok: false, detail: pageErrors.join(" | ") });
const failed = results.filter((x) => !x.ok);
if (failed.length) await context.tracing.stop({ path: fileURLToPath(new URL("trace-falha.zip", artifacts)) });
else await context.tracing.stop();
await browser.close();
const report = { timestamp: new Date().toISOString(), database: seed.database,
  frontend: origin.origin, publicUrl, passed: results.length - failed.length, failed: failed.length, results };
await writeFile(new URL("report.json", artifacts), JSON.stringify(report, null, 2));
console.log(`RESULTADO: ${report.passed}/${results.length} aprovados; ${report.failed} falhas.`);
console.log("Relatório: frontend/.homolog-artifacts/report.json (ignorado pelo Git)");
if (failed.length) process.exitCode = 1;
