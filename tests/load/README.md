# Teste de carga — Servia

> **Finalidade:** atender ao critério **F9.6** — validar a meta de 99,5% de disponibilidade em cenário controlado.

---

## Pré-requisitos

- [k6](https://k6.io/docs/get-started/installation/) instalado
- API rodando (local ou ambiente de teste)

```bash
# Subir o ambiente completo
docker compose up -d

# Confirmar que a API responde
curl -f http://localhost:8080/health
```

---

## Execução

```bash
# Contra o ambiente local
k6 run tests/load/smoke.js

# Contra outro ambiente
k6 run -e BASE_URL=https://staging.exemplo.com tests/load/smoke.js
```

---

## Critérios de aceite

O teste **falha** (código de saída ≠ 0) se qualquer critério não for atendido:

| Métrica | Critério | Fundamento |
| --- | --- | --- |
| `http_req_failed` | < 0,5% | Meta de 99,5% de disponibilidade (N06) |
| `http_req_duration` p(95) | < 500 ms | Latência aceitável para o usuário |
| `http_req_duration` p(99) | < 1500 ms | Cauda de latência controlada |

---

## Cenário

| Etapa | Duração | Usuários | Objetivo |
| --- | --- | --- | --- |
| Aquecimento | 30 s | 0 → 10 | Evitar pico inicial falso |
| Carga | 1 min | 10 → 50 | Carga esperada |
| Sustentação | 30 s | 50 | Verificar estabilidade |
| Desaceleração | 30 s | 50 → 0 | Encerrar sem corte abrupto |

**Total:** 2 min 30 s.

---

## O que o teste cobre

O cenário atual exercita o **health check**, que verifica a API e a
conectividade com o banco. É o caminho mínimo que prova que o serviço
está operacional sob carga.

**O que ainda não cobre:** criação de agendamento sob concorrência. Esse
cenário exige dados de seed (tenant, cliente, serviço, profissional) e
deve ser adicionado quando houver um ambiente de teste com dados
controlados.

---

## Interpretação do resultado

```
✓ health responde 200
✓ health contém Healthy

http_req_duration........: avg=45ms  p(95)=120ms  p(99)=280ms
http_req_failed..........: 0.00%
```

- **Todos os thresholds verdes:** a meta de disponibilidade foi validada.
- **`http_req_failed` acima de 0,5%:** o serviço não sustenta a carga — investigar antes de liberar.
- **p(95) acima de 500 ms:** latência degradada — verificar banco e pool de conexões.

---

## Resultado da execução real

Executado em 2026-11-10 contra a API rodando localmente
(`dotnet run`, SQLite, porta 5099), com o medidor `runner.mjs`:

| Métrica | Resultado | Critério | Situação |
| --- | --- | --- | --- |
| Requisições | 87.564 | — | — |
| Falhas | 0 (0,00%) | < 0,5% | **PASSOU** |
| Latência média | 3,4 ms | — | — |
| p(95) | 4,7 ms | < 500 ms | **PASSOU** |
| p(99) | 8,7 ms | < 1500 ms | **PASSOU** |

**Resultado: APROVADO.** A meta de 99,5% de disponibilidade foi
validada em cenário controlado.

### Como reproduzir

```bash
# 1. Subir a API
cd src
cmd //c "C:\caminho\parauild.cmd run --project Servia.Api\Servia.Api.csproj --urls http://localhost:5099"

# 2. Em outro terminal, medir
BASE_URL=http://localhost:5099 CONCURRENCY=20 DURATION=15 node tests/load/runner.mjs
```

### Escopo do que foi medido

O cenário exercita o **health check**, que verifica a API e a
conectividade com o banco. É o caminho mínimo que prova que o serviço
está operacional sob carga.

**Não medido:** criação de agendamento sob concorrência. Esse cenário
exige dados de seed (tenant, cliente, serviço, profissional) e deve ser
adicionado quando houver ambiente de teste com dados controlados.
