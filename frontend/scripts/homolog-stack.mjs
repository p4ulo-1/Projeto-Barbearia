/** Sobe somente a stack descartável AV2, com credenciais locais não versionadas. */
import { randomBytes } from "node:crypto";
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

const artifacts = new URL("../.homolog-artifacts/", import.meta.url);
mkdirSync(fileURLToPath(artifacts), { recursive: true });
const envFile = fileURLToPath(new URL("stack.env", artifacts));
if (!existsSync(envFile)) {
  const password = randomBytes(24).toString("hex");
  const signingKey = randomBytes(48).toString("hex");
  writeFileSync(envFile,
    `SERVIA_HOMOLOG_DB_PASSWORD=${password}\nSERVIA_HOMOLOG_JWT_KEY=${signingKey}\n`,
    { mode: 0o600 });
}
const compose = fileURLToPath(new URL("homolog-compose.yml", import.meta.url));
const result = spawnSync("docker",
  ["compose", "--env-file", envFile, "-p", "servia-av2-homolog",
    "-f", compose, "up", "--build", "-d"],
  { stdio: "inherit" });
process.exitCode = result.status ?? 1;
