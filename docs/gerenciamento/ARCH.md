# Arquitetura — Plataforma SaaS de Gestão e Atendimento

> **Status:** v1 aprovada em 2026-11-04. Decisões estruturais confirmadas pelo responsável do produto.
> **Escopo:** arquitetura de solução da plataforma multi-tenant, incluindo camadas, isolamento, integrações e operação.
> **Decisões:** ver seção 13 e `docs/gerenciamento/ADR.md`.

---

## 1. Visão geral

A plataforma é um SaaS multi-tenant que atende negócios de serviços agendados. A arquitetura parte do sistema atual (backend .NET 10 em camadas + frontend React) e evolui para suportar múltiplos estabelecimentos, site por tenant, billing, automação de WhatsApp e geração de site por IA.

### 1.1. Princípios arquiteturais

| # | Princípio | Aplicação |
| --- | --- | --- |
| A1 | **Isolamento por tenant** | Todo dado de negócio carrega `TenantId`; filtro global no EF Core |
| A2 | **Modularidade por contexto** | Contextos delimitados com fronteiras claras (Plataforma, Catálogo, Agenda, Financeiro, Site, IA) |
| A3 | **Configuração em vez de bifurcação** | Diferenças entre segmentos resolvidas por configuração e módulos, não por código duplicado |
| A4 | **Integrações atrás de abstrações** | Gateways, mensageria e LLM acessados por interfaces |
| A5 | **Aprovação humana em ações de IA** | Nenhuma alteração gerada por IA é publicada sem aprovação |
| A6 | **Segurança por padrão** | Falha fechada em fronteiras de confiança; autorização verificada no servidor |
| A7 | **Observabilidade desde o início** | Logs estruturados, métricas e trace de requisição |

---

## 2. Arquitetura de camadas (backend)

Mantém a estrutura atual, com interfaces de repositório e contexto de tenant.

```text
┌──────────────────────────────────────────────────────────────┐
│  Servia.Api (entrada HTTP)                                │
│  Controllers · Middleware · Auth · TenantResolution          │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Servia.Application (casos de uso)                        │
│  Services · DTOs · Validators · Orquestração de integrações   │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Servia.Domain (regras de negócio)                        │
│  Entidades · Agregados · Objetos de valor · Interfaces        │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Servia.Infrastructure (implementação)                    │
│  EF Core · Repositórios · Gateways · Mensageria · IA · JWT    │
└───────────────────────────┬──────────────────────────────────┘
                            │
                    ┌───────▼────────┐
                    │  PostgreSQL    │
                    └────────────────┘
```

### 2.1. Responsabilidades por camada

| Camada | Responsabilidade | Não deve conter |
| --- | --- | --- |
| **Api** | Roteamento, autenticação, resolução de tenant, tratamento de erro | Regra de negócio |
| **Application** | Casos de uso, transações, orquestração de integrações | Regra de domínio |
| **Domain** | Entidades, agregados, invariantes, interfaces de repositório | Dependência de EF Core ou HTTP |
| **Infrastructure** | Persistência, integrações externas, segurança | Regra de negócio |

### 2.2. Regra de dependência

`Api → Application → Domain` e `Infrastructure → Domain`. Domain não referencia nenhuma outra camada. Application não referencia Infrastructure diretamente; depende apenas de interfaces.

---

## 3. Isolamento multi-tenant

### 3.1. Estratégia

Banco único com `TenantId` (ADR-002). O isolamento é garantido em três níveis:

1. **Resolução do tenant** — `TenantResolutionMiddleware` identifica o tenant por subdomínio, por claim do JWT ou por header administrativo.
2. **Contexto de requisição** — `ITenantContext` expõe o `TenantId` corrente via `AsyncLocal`.
3. **Filtro global** — `AppDbContext` aplica `HasQueryFilter` em todas as entidades que implementam `ITenantScoped`.

```text
Requisição
   │
   ▼
TenantResolutionMiddleware ──► ITenantContext (TenantId)
   │
   ▼
Controller ──► Application Service ──► Repository
                                          │
                                          ▼
                              AppDbContext (filtro global)
                                          │
                                          ▼
                                    PostgreSQL
```

### 3.2. Regras de segurança

- Toda entidade de negócio implementa `ITenantScoped` com `TenantId`.
- `IgnoreQueryFilters()` é proibido fora de casos administrativos explícitos e revisados.
- Teste automatizado no pipeline falha se o filtro global for removido.
- Operações administrativas da plataforma usam um contexto explícito de `PlatformAdmin`, nunca um tenant arbitrário.

### 3.3. Riscos e mitigação

| Risco | Mitigação |
| --- | --- |
| Vazamento entre tenants | Testes de isolamento obrigatórios; revisão de código em qualquer uso de `IgnoreQueryFilters` |
| Consulta cross-tenant acidental | Repositórios só expõem métodos que aplicam o filtro |
| Tenant suspenso acessando dados | Verificação de status do tenant no middleware |

---

## 4. Frontend

### 4.1. Estrutura

Mantém React 19 + TypeScript + TanStack Start/Router + Tailwind 4 + shadcn, com separação por superfícies de produto.

| Superfície | Rota base | Público |
| --- | --- | --- |
| Site público do tenant | `{slug}.plataforma.com` | Clientes finais |
| Agendamento | `{slug}.plataforma.com/agendamento` | Clientes finais |
| Painel do estabelecimento | `app.plataforma.com` | Equipe do tenant |
| Painel da plataforma | `admin.plataforma.com` | Administradores da plataforma |

### 4.2. Decisões de frontend

| # | Decisão | Justificativa |
| --- | --- | --- |
| F1 | Renderização do site por tenant no servidor | SEO e tempo de primeira renderização |
| F2 | Componentes de site vindos da biblioteca da plataforma | Consistência e integração com agenda |
| F3 | Estado por contexto + TanStack Query | Já adotado no projeto; evita dependência extra |
| F4 | Autenticação com refresh token em cookie `HttpOnly` | Reduz risco de XSS em relação a `localStorage` |
| F5 | Guardas de rota por papel | Evita acesso indevido no cliente; servidor continua validando |

### 4.3. Migração do frontend atual

| Hoje | Destino |
| --- | --- |
| `index.tsx` (landing estática) | Site público gerado por tenant |
| `agendamento.tsx` | Agendamento dentro do site do tenant |
| `auth.tsx` | Login unificado (tenant e plataforma) |
| `conta.tsx` | Área do cliente final |
| `admin.tsx` | Painel do estabelecimento |

---

## 5. Integrações externas

### 5.1. Pagamentos

```text
Application ──► IPaymentGateway ──► MercadoPagoGateway
```

- **Gateway confirmado:** somente Mercado Pago (Pix, boleto e cartão). A abstração `IPaymentGateway` permite adicionar Stripe no futuro sem alterar casos de uso.
- Contrato único: criar cobrança, criar assinatura, estornar, processar webhook.
- Webhooks idempotentes por `GatewayPaymentId`.
- Nenhum caso de uso referencia SDK de gateway diretamente.

### 5.2. Mensageria (WhatsApp)

```text
Application ──► IMessagingProvider ──┬──► MetaCloudApiProvider
                                     └──► (provedor alternativo)
```

- Contrato: enviar mensagem, receber webhook, validar template.
- Templates aprovados pela Meta são versionados por tenant.

### 5.3. IA de site

```text
Application ──► IAiSiteGenerator ──► Provedor de LLM
```

- Entrada: dados do tenant, referências e prompt.
- Saída: `AiChangeSet` com snapshot proposto, nunca aplicado diretamente.
- Guardrails: validação de schema dos componentes e bloqueio de conteúdo inventado.
- Controle de custo: teto por requisição e por tenant.

### 5.4. Notificações

```text
Application ──► INotificationSender ──┬──► E-mail
                                     ├──► WhatsApp
                                     └──► SMS (opcional)
```

---

## 6. Modelo de dados e persistência

### 6.1. Convenções

- Chaves primárias `Guid` geradas na aplicação.
- `TenantId` obrigatório em entidades de negócio.
- `CreatedAt` e `UpdatedAt` em `DateTimeOffset` (UTC).
- Soft delete apenas onde o histórico é obrigatório (serviços, profissionais, clientes).
- `jsonb` para configuração de seções do site, snapshots de IA e payloads de notificação.

### 6.2. Índices críticos

| Tabela | Índice | Motivo |
| --- | --- | --- |
| `Appointments` | `(TenantId, ProfessionalId, Date, Time)` | Consulta de agenda e detecção de conflito |
| `Customers` | `(TenantId, Phone)` | Busca por telefone no atendimento |
| `Users` | `(TenantId, Email)` único | Login por tenant |
| `Tenants` | `Slug` único | Resolução por subdomínio |
| `Payments` | `(TenantId, GatewayPaymentId)` único | Idempotência de webhook |
| `AuditEntries` | `(TenantId, OccurredAt)` | Consulta de auditoria por período |

### 6.3. Migrations

- Uma migration por mudança de schema, aplicada em todos os tenants.
- Migrations destrutivas exigem backup prévio e aprovação.
- Seed de dados de demonstração apenas em ambiente de desenvolvimento.

---

## 7. Segurança

| Área | Decisão |
| --- | --- |
| Senhas | Hash com `PasswordHasher` do ASP.NET Core; rehash no login quando o algoritmo muda |
| Sessão | JWT de acesso curto + refresh token revogável em cookie `HttpOnly` |
| Autorização | Verificação por papel e por recurso no servidor; nunca só no cliente |
| Transporte | HTTPS obrigatório; HSTS em produção |
| CSRF | Token anti-CSRF nas rotas que usam cookie |
| Entrada | Validação com DataAnnotations e validação de schema para configuração de componentes |
| Segredos | Variáveis de ambiente; nunca em repositório |
| Auditoria | `AuditEntry` imutável para ações sensíveis |

---

## 8. Observabilidade e operação

| Item | Implementação |
| --- | --- |
| Logs | Estruturados em JSON com `TenantId`, `TraceId` e `UserId` |
| Métricas | Latência por endpoint, taxa de erro, jobs em atraso |
| Trace | Correlação de requisição entre API, banco e integrações |
| Health check | `/health` com estado de API, banco e integrações críticas |
| Alertas | Erro 5xx acima do limite, indisponibilidade, fila de notificação travada |
| Backup | Diário com retenção definida; restauração testada por período |
| Runbook | Procedimentos de incidente documentados na fase F9 |

---

## 9. Estratégia de testes

| Nível | Escopo | Ferramenta |
| --- | --- | --- |
| Domínio | Invariantes de agregados e objetos de valor | xUnit (já existente) |
| Aplicação | Casos de uso com repositórios dublês | xUnit + dublês |
| Integração | Repositórios, filtro de tenant e migrations | xUnit + Testcontainers (PostgreSQL) |
| Contrato | Implementações de `IPaymentGateway` e `IMessagingProvider` | Testes de contrato por implementação |
| E2E | Fluxo de cadastro, agendamento e cobrança | Playwright |
| Segurança | Isolamento entre tenants | Teste dedicado que falha se o filtro for removido |

---

## 10. Pipeline de entrega

```text
Push / PR
   │
   ▼
Build (backend + frontend)
   │
   ▼
Testes (domínio, aplicação, integração)
   │
   ▼
Análise estática e lint
   │
   ▼
Build de imagem Docker
   │
   ▼
Deploy em ambiente de staging
   │
   ▼
Smoke tests
   │
   ▼
Deploy em produção (aprovação manual)
```

---

## 11. Estrutura de repositório proposta

```text
Projeto-Barbearia/
├── backend/
│   ├── Servia.Api/
│   ├── Servia.Application/
│   ├── Servia.Domain/
│   ├── Servia.Infrastructure/
│   └── Servia.Tests/
├── frontend/
│   ├── src/
│   │   ├── routes/            # site público, agendamento, painel, admin
│   │   ├── components/site/   # biblioteca de componentes do site
│   │   └── services/          # cliente de API
├── docs/
│   ├── descricao_produto.md
│   ├── levantamento_lacunas.md   # inventário do código legado (insumo de F1.4)
│   ├── funcionalidades.md
│   ├── classes.md
│   └── gerenciamento/
│       ├── Roadmap.md
│       ├── ADR.md
│       └── ARCH.md
├── docker-compose.yml
└── .github/workflows/
```

---

## 12. Riscos arquiteturais

| # | Risco | Impacto | Mitigação |
| --- | --- | --- | --- |
| R1 | Vazamento de dados entre tenants | Crítico | Filtro global + testes de isolamento no pipeline |
| R2 | Custo de LLM fora de controle | Alto | Teto por requisição e por tenant; alerta de consumo |
| R3 | Aprovação de templates do WhatsApp atrasar a fase F6 | Médio | Iniciar verificação na fase F0 |
| R4 | Migração de SQLite para PostgreSQL com perda de dados | Alto | Migração em ambiente isolado com validação de contagem |
| R5 | Acoplamento do frontend ao modelo single-shop | Médio | Migração por superfície de produto, com rotas separadas |
| R6 | Explosão de módulos por segmento | Médio | Configuração antes de código; módulo novo exige justificativa de reuso |
| R7 | Webhooks duplicados ou fora de ordem | Alto | Idempotência por identificador do gateway |
| R8 | Dependência de provedor único de IA ou mensageria | Médio | Interfaces de abstração com implementação alternativa |

---

## 13. Decisões confirmadas

Todas as decisões estruturais foram confirmadas pelo responsável do produto em 2026-11-04.

| # | Decisão | Escolha confirmada | Impacto |
| --- | --- | --- | --- |
| D1 | Reaproveitamento do código como primeiro tenant (ADR-001) | ✅ Reaproveitar | Fase F1 |
| D2 | Banco único com `TenantId` (ADR-002) | ✅ Banco único | Modelo de dados |
| D3 | PostgreSQL em produção (ADR-003) | ✅ PostgreSQL | Fase F0 |
| D4 | Gateway de pagamento (ADR-005) | ✅ Somente Mercado Pago | Fase F4 |
| D5 | Cloud API da Meta (ADR-006) | ✅ Cloud API oficial | Fase F6 |
| D6 | Provedor de LLM e teto de custo (ADR-007) | ⏳ Definir na fase F8 (abstração `IAiSiteGenerator`) | Fase F8 |
| D7 | PaaS e provedor de hospedagem (ADR-011) | ✅ Docker + GitHub Actions + PaaS | Fase F0 |
| D8 | Modelo LGPD controlador/operadora (ADR-012) | ✅ Tenant controlador, plataforma operadora | Fase F9 |
| D9 | Modelo de planos e regra de cobrança | ✅ 4 planos por faixa de profissionais; excedente exige upgrade | Fase F4 |
| D10 | Domínio próprio no MVP | ✅ Apenas subdomínio no MVP | Fase F3 |

### 13.1. Modelo de planos confirmado

**Todos os planos oferecem todos os serviços e módulos.** A diferenciação ocorre por quantidade de profissionais e grau de personalização do site.

| Plano | Profissionais incluídos | Módulos | Personalização do site |
| --- | --- | --- | --- |
| Essencial | até 3 | Todos | Básica |
| Pro | até 7 | Todos | Intermediária |
| Premium | até 15 | Todos | Avançada |
| Business | ilimitado | Todos | Total |

**Regra de profissionais:** não há taxa por excedente. Ao ultrapassar o limite do plano, o tenant precisa subir de categoria.

**Regra de site:** a geração inicial por IA respeita o limite de personalização do plano. **Alterações posteriores ao lançamento são cobradas à parte**, independentemente do plano.

### 13.2. Regra de trial e inadimplência confirmada

1. Trial de **14 dias** com cartão de crédito cadastrado.
2. Ao fim do trial, o cartão é debitado automaticamente.
3. Inadimplência: **3 dias de aviso com acesso total**.
4. Depois: **bloqueio de escrita**, com leitura e agenda disponíveis por **15 dias**.
5. Após 15 dias de bloqueio: suspensão total do tenant.

### 13.3. Pendência remanescente

| # | Pendência | Quando resolver |
| --- | --- | --- |
| D6 | Provedor de LLM e teto de custo por geração | Fase F8, após comparar custo real |
