/** Executa seed e E2E sem imprimir credenciais aleatórias no terminal. */
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

if (!process.env.SERVIA_SMOKE_BASE_URL)
  throw new Error("Defina SERVIA_SMOKE_BASE_URL com a origem local do Vite.");
const artifacts = fileURLToPath(new URL("../.homolog-artifacts/", import.meta.url));
mkdirSync(artifacts, { recursive: true });
const seedFile = fileURLToPath(new URL("seed.json", new URL("../.homolog-artifacts/", import.meta.url)));
const seedOutput = execFileSync(process.execPath,
  [fileURLToPath(new URL("homolog-seed.mjs", import.meta.url))],
  { encoding: "utf8", env: process.env });
const seed = JSON.parse(seedOutput);
writeFileSync(seedFile, seedOutput, { mode: 0o600 });
console.log(`Seed isolado: tenant=${seed.tenantId}; banco=${seed.database}; data=${seed.date}`);
const run = spawnSync(process.execPath,
  [fileURLToPath(new URL("homolog-e2e.mjs", import.meta.url))],
  { stdio: "inherit", env: { ...process.env, SERVIA_HOMOLOG_SEED_FILE: seedFile } });
process.exitCode = run.status ?? 1;
