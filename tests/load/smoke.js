// ============================================================
// Teste de carga do Servia (F9.6)
//
// Valida a meta de disponibilidade e latência em cenário controlado.
// Executado com k6: https://k6.io
//
// Uso:
//   k6 run tests/load/smoke.js
//   k6 run -e BASE_URL=http://localhost:8080 tests/load/smoke.js
//
// Critérios de aceite (thresholds):
//   - 99,5% das requisições com sucesso (meta de disponibilidade)
//   - 95% das requisições abaixo de 500 ms
//   - 99% das requisições abaixo de 1500 ms
//
// Se qualquer threshold falhar, o k6 retorna código de saída diferente
// de zero e o pipeline falha.
// ============================================================

import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  // Cenário controlado: carga crescente até 50 usuários simultâneos.
  stages: [
    { duration: '30s', target: 10 },  // aquecimento
    { duration: '1m', target: 50 },   // carga esperada
    { duration: '30s', target: 50 },  // sustentação
    { duration: '30s', target: 0 },   // desaceleração
  ],

  thresholds: {
    // Meta de disponibilidade: 99,5% de sucesso.
    http_req_failed: ['rate<0.005'],

    // Latência: 95% abaixo de 500 ms, 99% abaixo de 1500 ms.
    http_req_duration: ['p(95)<500', 'p(99)<1500'],
  },
};

export default function () {
  // Health check: verifica API e banco em cada iteração.
  const health = http.get(`${BASE_URL}/health`);

  check(health, {
    'health responde 200': (response) => response.status === 200,
    'health contém Healthy': (response) =>
      response.body && response.body.includes('Healthy'),
  });

  // Pausa entre iterações, simulando usuário real.
  sleep(1);
}
