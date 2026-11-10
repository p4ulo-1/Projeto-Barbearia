// ============================================================
// Medidor de carga do Servia (F9.6)
//
// Alternativa ao k6 quando ele não está disponível no ambiente.
// Usa apenas a biblioteca padrão do Node — sem dependências.
//
// Uso:
//   node tests/load/runner.mjs
//   BASE_URL=http://localhost:5099 node tests/load/runner.mjs
//
// Critérios de aceite (mesmos do smoke.js):
//   - 99,5% das requisições com sucesso
//   - p(95) abaixo de 500 ms
//   - p(99) abaixo de 1500 ms
//
// Retorna código de saída 1 se algum critério falhar.
// ============================================================

const BASE_URL = process.env.BASE_URL || 'http://localhost:5099';
const CONCURRENCY = Number(process.env.CONCURRENCY || 20);
const DURATION_SECONDS = Number(process.env.DURATION || 15);

const THRESHOLDS = {
  maxFailureRate: 0.005, // 99,5% de sucesso
  p95Ms: 500,
  p99Ms: 1500,
};

/** Executa uma requisição e devolve o resultado medido. */
async function request() {
  const startedAt = performance.now();

  try {
    const response = await fetch(`${BASE_URL}/health`);
    const body = await response.text();
    const durationMs = performance.now() - startedAt;

    return {
      ok: response.status === 200 && body.includes('Healthy'),
      status: response.status,
      durationMs,
    };
  } catch (error) {
    return {
      ok: false,
      status: 0,
      durationMs: performance.now() - startedAt,
      error: error.message,
    };
  }
}

/** Calcula um percentil sobre uma lista ordenada de durações. */
function percentile(sorted, value) {
  if (sorted.length === 0) return 0;

  const index = Math.ceil((value / 100) * sorted.length) - 1;
  return sorted[Math.max(0, index)];
}

async function main() {
  console.log(`Alvo: ${BASE_URL}/health`);
  console.log(`Concorrência: ${CONCURRENCY} | Duração: ${DURATION_SECONDS}s\n`);

  const results = [];
  const deadline = Date.now() + DURATION_SECONDS * 1000;

  // Cada worker executa requisições até o prazo terminar.
  async function worker() {
    while (Date.now() < deadline) {
      results.push(await request());
    }
  }

  await Promise.all(Array.from({ length: CONCURRENCY }, () => worker()));

  const total = results.length;
  const failures = results.filter((result) => !result.ok).length;
  const failureRate = total === 0 ? 1 : failures / total;

  const durations = results.map((result) => result.durationMs).sort((a, b) => a - b);
  const average = durations.reduce((sum, value) => sum + value, 0) / (total || 1);

  const p95 = percentile(durations, 95);
  const p99 = percentile(durations, 99);

  console.log(`Requisições: ${total}`);
  console.log(`Falhas: ${failures} (${(failureRate * 100).toFixed(2)}%)`);
  console.log(`Latência média: ${average.toFixed(1)} ms`);
  console.log(`p(95): ${p95.toFixed(1)} ms`);
  console.log(`p(99): ${p99.toFixed(1)} ms\n`);

  const checks = [
    ['Taxa de falha < 0,5%', failureRate < THRESHOLDS.maxFailureRate],
    ['p(95) < 500 ms', p95 < THRESHOLDS.p95Ms],
    ['p(99) < 1500 ms', p99 < THRESHOLDS.p99Ms],
  ];

  let allPassed = true;

  for (const [label, passed] of checks) {
    console.log(`${passed ? 'PASSOU' : 'FALHOU'}  ${label}`);
    allPassed = allPassed && passed;
  }

  console.log(`\nResultado: ${allPassed ? 'APROVADO' : 'REPROVADO'}`);
  process.exit(allPassed ? 0 : 1);
}

await main();
