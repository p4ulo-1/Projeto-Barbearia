# Runbook Operacional — Servia

> **Finalidade:** atender ao critério **F9.7** — documentação operacional de incidentes.
> **Público:** quem está de plantão e precisa agir sob pressão.
> **Princípio:** cada procedimento começa pelo sintoma, não pela arquitetura.

---

## 1. Contatos e escalonamento

| Nível | Responsável | Quando acionar |
| --- | --- | --- |
| L1 | Plantão técnico | Todo incidente |
| L2 | Responsável pela plataforma | Após 30 min sem resolução |
| L3 | Proprietário do produto | Decisão de negócio, comunicação a clientes |

**Comunicação a clientes:** apenas L3 autoriza. Nunca prometer prazo sem confirmação técnica.

---

## 2. Diagnóstico rápido

Antes de qualquer ação, responda três perguntas:

1. **O serviço está no ar?** `curl -f https://<host>/health`
2. **O banco responde?** O `/health` já verifica a conectividade.
3. **É um tenant ou todos?** Verifique o `TraceId` no log e o `TenantId` afetado.

```bash
# Estado do serviço
curl -s https://<host>/health

# Últimos erros (se houver agregador de logs)
# Filtrar por TraceId do relato do cliente
```

---

## 3. Incidentes

### 3.1. Serviço indisponível (health check falhando)

**Sintoma:** `/health` retorna 503 ou não responde.

| Passo | Ação | Verificação |
| --- | --- | --- |
| 1 | Verificar se o processo está rodando | `docker compose ps` |
| 2 | Verificar logs do container | `docker compose logs --tail=100 api` |
| 3 | Verificar o banco | `docker compose logs --tail=50 database` |
| 4 | Se o banco estiver fora, reiniciar | `docker compose restart database` |
| 5 | Se a API estiver fora, reiniciar | `docker compose restart api` |
| 6 | Confirmar recuperação | `curl -f https://<host>/health` |

**Se persistir:** escalar para L2. Não reiniciar repetidamente sem investigar a causa.

### 3.2. Erro 500 em massa

**Sintoma:** taxa de erro acima do limite; alerta `ErrorRate` disparado.

| Passo | Ação |
| --- | --- |
| 1 | Coletar o `TraceId` de uma requisição que falhou |
| 2 | Buscar o `TraceId` no log — a mensagem de erro está registrada com o detalhe |
| 3 | Verificar se o erro começou após um deploy |
| 4 | Se sim, avaliar rollback (seção 4) |
| 5 | Se não, investigar a causa antes de agir |

**O `TraceId` é a chave.** A resposta ao cliente traz apenas uma mensagem genérica; o detalhe está no log, indexado pelo mesmo identificador.

### 3.3. Tenant específico com problema

**Sintoma:** um estabelecimento relata falha; os demais operam normalmente.

| Passo | Ação |
| --- | --- |
| 1 | Confirmar o `TenantId` afetado |
| 2 | Verificar o status do tenant (`Trial`, `Active`, `Suspended`, `Cancelled`) |
| 3 | Se `Suspended`, verificar a régua de inadimplência |
| 4 | Se `Active`, investigar os dados do tenant |

**Atenção:** o filtro global do EF Core isola os tenants. Se um tenant vê dados de outro, é **incidente de segurança** — escalar imediatamente para L2.

### 3.4. WhatsApp não envia

**Sintoma:** mensagens não chegam aos clientes.

| Passo | Ação |
| --- | --- |
| 1 | Verificar a operação como Solution Partner: aprovação e linha de crédito |
| 2 | Verificar a cota do tenant (`UsageQuota` de `WhatsAppMessages`) |
| 3 | Verificar se o tipo de mensagem está autorizado (`MessagingPolicy`) |
| 4 | Verificar o log de envio no provedor |

**Causas comuns:** cota esgotada, tipo não autorizado (marketing é opt-in), linha de crédito revogada.

### 3.5. IA de site falhando

**Sintoma:** geração não conclui ou retorna erro.

| Passo | Ação |
| --- | --- |
| 1 | Verificar a franquia de IA do tenant (`UsageQuota` de `AiUnits`) |
| 2 | Verificar o `AiOperationLog` — a falha registra o motivo |
| 3 | Verificar a disponibilidade do provedor de LLM |

**Fallback:** quando a IA falha, o site usa o modelo padrão. O cliente não fica sem site.

---

## 4. Deploy e rollback

### 4.1. Deploy

```bash
git pull origin main
docker compose build
docker compose up -d
curl -f https://<host>/health
```

**Critério de sucesso:** `/health` retorna 200 e os testes do pipeline estão verdes.

### 4.2. Rollback

```bash
git log --oneline -5          # identificar o commit anterior
git checkout <commit-anterior>
docker compose build
docker compose up -d
curl -f https://<host>/health
```

**Quando fazer rollback:** erro 500 em massa iniciado após o deploy, ou health check falhando.

**Quando NÃO fazer:** erro que já existia antes do deploy. Rollback não corrige causa antiga.

---

## 5. Banco de dados

### 5.1. Backup

O backup é **por tenant**. Restaurar um estabelecimento não afeta os demais.

```bash
# Backup completo (infraestrutura)
docker compose exec database pg_dump -U servia servia > backup-$(date +%F).sql
```

**Verificação:** o backup tem checksum registrado em `TenantBackup`. Um backup sem checksum não é confiável.

### 5.2. Restauração

```bash
docker compose exec -T database psql -U servia servia < backup-2026-11-10.sql
```

**Regra:** restaurar apenas no próprio tenant de origem. Restaurar dados de um tenant em outro viola o isolamento.

---

## 6. LGPD

### 6.1. Solicitação do titular

| Tipo | Prazo | Ação |
| --- | --- | --- |
| Acesso | 15 dias | Exportar os dados do titular |
| Correção | 15 dias | Corrigir os dados apontados |
| Exclusão | 15 dias | Anonimizar os dados pessoais |
| Portabilidade | 15 dias | Exportar em formato legível |

**A exclusão não apaga o histórico financeiro.** Os dados pessoais são anonimizados; os lançamentos permanecem para integridade contábil.

**Papéis:** o estabelecimento é o **controlador**; a plataforma é a **operadora**.

### 6.2. Retenção

| Categoria | Prazo | Fundamento |
| --- | --- | --- |
| Financeiro | 5 anos | Prazo fiscal |
| Auditoria | 2 anos | Rastreabilidade |
| Operacional | 1 ano | Necessidade do serviço |
| Mensageria | 180 dias | Necessidade do serviço |
| Logs de IA | 90 dias | Necessidade do serviço |

---

## 7. O que NUNCA fazer

| Ação | Por quê |
| --- | --- |
| Desativar o filtro global de tenant | Vaza dados entre estabelecimentos |
| Restaurar backup em outro tenant | Viola o isolamento |
| Apagar `AuditEntry` | Destrói a trilha de auditoria |
| Reiniciar em loop sem investigar | Mascara a causa e agrava o incidente |
| Prometer prazo a cliente sem confirmação técnica | Cria expectativa que pode não ser cumprida |
| Commitar com `--no-verify` | Ignora os hooks de validação |

---

## 8. Verificação pós-incidente

Após resolver, registre:

1. **O que aconteceu** — sintoma e impacto
2. **Quando começou e terminou** — duração
3. **Causa raiz** — não o sintoma
4. **O que foi feito** — ações tomadas
5. **O que evita a repetição** — correção sistêmica, não paliativo

**Sem o item 5, o incidente vai voltar.**
