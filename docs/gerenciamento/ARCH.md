# Arquitetura — Plataforma SaaS de Gestão e Atendimento

> **Status:** v3 — implementação concluída em 2026-11-11.
> **Escopo:** arquitetura de solução da plataforma multi-tenant, incluindo camadas, isolamento, integrações e operação.
> **Decisões:** ver seção 13 e `docs/gerenciamento/ADR.md`.

---

## 0. Estado da implementação

A arquitetura descrita neste documento **está implementada**. O que segue é o registro do que existe, não uma proposta.

| Camada | Projeto | Estado |
| --- | --- | --- |
| Domínio | `Servia.Domain` | 37 entidades `ITenantScoped`, 14 contextos delimitados |
| Aplicação | `Servia.Application` | Casos de uso, sem dependência de EF Core |
| Infraestrutura | `Servia.Infrastructure` | EF Core 10 + Npgsql, repositórios, integrações |
| API | `Servia.Api` | Controllers, middleware, autenticação, autorização |
| Testes | `Servia.Tests` | 837 testes, todos em PostgreSQL 17 |
| Frontend | `web/` | React 19 + TypeScript estrito + Vite |

### Isolamento multi-tenant — três camadas

| Camada | Mecanismo | Onde |
| --- | --- | --- |
| 1. Aplicação | Filtro global do EF Core | `AppDbContext.ApplyTenantFilter` |
| 2. Banco | Chaves estrangeiras compostas `(TenantId, Id)` | `CompositeForeignKeyConfiguration` |
| 3. Testes | Suíte cross-tenant via API | `Servia.Tests/Integration/` |

**Nenhuma das três é suficiente sozinha.** O filtro global não protege escrita; a FK não protege leitura; os testes provam que as duas funcionam juntas.

### Persistência

- **PostgreSQL 17** é o único provider — desenvolvimento, testes e produção.
- Migrations versionadas (`MigrateAsync`), nunca `EnsureCreated`.
- 3 migrations aplicadas: `InitialCreate`, `CompositeForeignKeys`, `PartialScheduleLockIndex`.
- 50 tabelas, 27 chaves estrangeiras, 117 índices.

### Concorrência

`AppointmentService` executa verificação e gravação na mesma transação com isolamento **serializável**. `ScheduleLock` usa índice único parcial filtrado por `IsActive` — o PostgreSQL exige predicado `IMMUTABLE`, e `now()` não é.

---

---

## 1. Visão geral

A plataforma é um SaaS multi-tenant que atende negócios de serviços agendados. A arquitetura parte do sistema atual (backend .NET 10 em camadas + frontend React) e evolui para suportar múltiplos estabelecimentos, site por tenant, billing, automação de WhatsApp e geração de site por IA.

### 1.1. Princípios arquiteturais

| # | Princípio | Aplicação |
| --- | --- | --- |
| A1 | **Isolamento por tenant** | Todo dado de negócio carrega `TenantId`; isolamento aplicado em **todas as camadas** (API, aplicação, domínio e persistência) |
| A2 | **Modularidade por contexto** | Contextos delimitados com fronteiras claras (Plataforma, Catálogo, Agenda, Financeiro, Site, IA) |
| A3 | **Configuração em vez de bifurcação** | Diferenças entre segmentos resolvidas por configuração, módulos e **políticas injetadas**, nunca por condicionais de segmento no núcleo |
| A8 | **Núcleo universal** | Funcionalidades compartilhadas num núcleo único; particularidades por módulos, políticas e polimorfismo |
| A9 | **Contextos financeiros independentes** | Financeiro do tenant e billing da plataforma são domínios separados, com agregados e registros próprios |
| A10 | **Consistência sob concorrência** | A reserva de horário é garantida no banco, não apenas por consulta prévia de disponibilidade |
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

### 3.2. Isolamento em todas as camadas

O isolamento é requisito **fundamental e inegociável** (diretrizes §3). Nenhum usuário de um estabelecimento acessa dados de outro, **independentemente de papel, nível de autorização ou privilégio dentro do próprio tenant**. Por isso o controle não fica só no filtro de consulta:

| Camada | Mecanismo |
| --- | --- |
| **API** | `TenantResolutionMiddleware` resolve o tenant **no servidor**, a partir do subdomínio ou da claim do JWT — nunca de um valor livre enviado pelo frontend |
| **Aplicação** | `ITenantMembershipValidator` confirma que o usuário autenticado **pertence** ao tenant em que tenta atuar, antes de qualquer operação |
| **Domínio** | Agregados recebem o `TenantId` do contexto e rejeitam operação cujo tenant não coincida com o do agregado |
| **Persistência** | Filtro global (`HasQueryFilter`) na leitura **e** verificação de `TenantId` na escrita; chaves estrangeiras **compostas com `TenantId`** impedem associação entre registros de tenants diferentes |

### 3.3. Restrições de integridade no banco

O banco é a última linha de defesa. Relacionamentos entre entidades de negócio usam **chave estrangeira composta incluindo `TenantId`**, de modo que o próprio PostgreSQL rejeite a associação de um registro de um tenant a um registro de outro.

```text
Appointments (TenantId, CustomerId) ──► Customers (TenantId, Id)
Appointments (TenantId, ProfessionalId) ──► Professionals (TenantId, Id)
```

Isso torna o vazamento por escrita **impossível no nível do banco**, mesmo que uma camada superior falhe.

### 3.4. Identidade independente por tenant

Não existe identidade global de usuário (diretrizes §3.1). A mesma pessoa física que frequenta dois estabelecimentos possui **dois cadastros `User` independentes**, sem vínculo e sem relacionamento automático. Consequências arquiteturais:

- O e-mail é único **por tenant**, não globalmente.
- Não há tabela de identidade compartilhada entre tenants.
- O login é resolvido no contexto do tenant (subdomínio ou claim), nunca por uma busca global por e-mail.
- Administradores da plataforma são a única exceção: `TenantId` nulo e papel `PlatformAdmin`, com privilégios **distintos** das permissões de tenant.

### 3.5. Regras de segurança

- Toda entidade de negócio implementa `ITenantScoped` com `TenantId`.
- `IgnoreQueryFilters()` é proibido fora de casos administrativos explícitos e revisados.
- Teste automatizado no pipeline falha se o filtro global for removido.
- Operações administrativas da plataforma usam um contexto explícito de `PlatformAdmin`, nunca um tenant arbitrário.
- Testes de isolamento tentam **consultar, alterar e excluir** dados de outro tenant e comprovam a rejeição.

### 3.6. Riscos e mitigação

| Risco | Mitigação |
| --- | --- |
| Vazamento entre tenants | Testes de isolamento obrigatórios; FK composta com `TenantId`; revisão de código em qualquer uso de `IgnoreQueryFilters` |
| Consulta cross-tenant acidental | Repositórios só expõem métodos que aplicam o filtro |
| Tenant suspenso acessando dados | Verificação de status do tenant no middleware |
| Tenant forjado pelo cliente | Tenant resolvido no servidor; valor enviado pelo frontend é ignorado |
| Usuário atuando em tenant alheio | `ITenantMembershipValidator` antes de toda operação |

---

## 3A. Núcleo universal e especialização por segmento

O núcleo é **universal** e não contém condicionais que identifiquem o segmento (diretrizes §2). As particularidades entram por **políticas injetadas** e **módulos**, acessados por interfaces.

```text
Application ──► IBookingPolicy ──┬──► DefaultBookingPolicy
                                 ├──► DepositBookingPolicy
                                 └──► LongSessionBookingPolicy

Application ──► IAvailabilityPolicy ──┬──► FixedSlotPolicy
                                      └──► VariableDurationPolicy
```

| Política | Decide |
| --- | --- |
| `IBookingPolicy` | Antecedência, sinal, no-show, cancelamento |
| `IAvailabilityPolicy` | Granularidade, duração variável, múltiplos blocos |
| `ICommissionPolicy` | Cálculo de comissão |
| `IBenefitPolicy` | Consumo de clube e pacote |

**Regra de extensão:** um novo segmento é atendido por configuração ou por uma nova implementação de política. Um `if (tenant.Segment == ...)` no núcleo é um defeito de arquitetura.

---

## 3B. Concorrência no agendamento

A verificação de disponibilidade **não** pode ser a única garantia (diretrizes §4.1). Duas solicitações simultâneas para o mesmo profissional e horário precisam resultar em **uma única** reserva confirmada.

```text
1. Consulta de disponibilidade (pré-validação, não vinculante)
2. BEGIN TRANSACTION
3. INSERT ScheduleLock (TenantId, ProfessionalId, Date, StartTime)
      └─ índice único parcial → segunda transação falha aqui
4. INSERT Appointment
5. COMMIT
```

| Mecanismo | Papel |
| --- | --- |
| Índice único parcial em `ScheduleLock` | Impede duas travas ativas para o mesmo profissional e horário |
| Transação | Garante que trava e agendamento entram juntos ou não entram |
| Expiração da trava | Evita bloqueio permanente se a transação não concluir |
| Teste de concorrência | Dispara N reservas simultâneas e exige exatamente 1 sucesso |

A consulta prévia continua existindo para a experiência do usuário, mas **nunca** é a garantia de consistência.

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

Application ──► IUsageQuotaService ──► UsageQuota (por tenant e período)
Application ──► IConsumptionBillingService ──► linha de cobrança WhatsApp
```

- Contrato: enviar mensagem, receber webhook, validar template.
- Templates aprovados pela Meta são versionados por tenant.
- **Limite de consumo** (diretrizes §7.1): todo envio passa por `IUsageQuotaService.TryConsume` **antes** de chamar o provedor. Ao atingir o limite, o envio é bloqueado no servidor e registrado para auditoria.
- Falhas do provedor são tratadas com repetição limitada e registro; resposta inesperada não corrompe o estado do agendamento.

#### 5.2.1. Operação como Solution Partner

O Servia **administra a integração e a operação** do WhatsApp e **centraliza o pagamento à Meta** pelo modelo **Solution Partner**.

```text
Estabelecimento ──► Servia (Solution Partner) ──► Meta
                        │
                        └── paga o consumo à Meta
```

| Aspecto | Decisão |
| --- | --- |
| Contratação | O estabelecimento **não** contrata a Meta; consome o serviço operado pelo Servia |
| Pagamento à Meta | Centralizado pelo Servia |
| Pré-requisito | **Aprovação** como Solution Partner e **linha de crédito** junto à Meta |
| Bloqueio | Sem aprovação e crédito, a fase F6 não entra em produção |

#### 5.2.2. Consumo tarifado, sem subsídio

- Cada estabelecimento **paga pelo consumo tarifado que gerar**.
- **Não há subsídio**: mensagens com custo **não** são incluídas gratuitamente nos planos.
- O **uso gratuito da Meta é aproveitado dentro dos planos** do Servia — a franquia gratuita é repassada ao tenant, não retida pela plataforma.

#### 5.2.3. Modalidades de cobrança

| Modalidade | Fluxo |
| --- | --- |
| **Cobrança automática** | Consumo apurado no período → cobrado na fatura seguinte |
| **Compra antecipada de créditos** | `CreditPurchase` pago → créditos aplicados à `UsageQuota` → consumo debita os créditos |

O estabelecimento escolhe a modalidade por configuração (`UsageQuota.BillingMode`).

#### 5.2.4. Controle de gastos pelo estabelecimento

- `UsageQuota.TenantLimit` define um teto próprio, além da franquia.
- Permissões por **tipo de mensagem** definem o que pode ser enviado e por quem.
- Ao atingir o limite, o envio é bloqueado no servidor.

### 5.3. IA de site

```text
Application ──► IAiSiteGenerator ──► Provedor de LLM

Application ──► IUsageQuotaService ──► UsageQuota (AiUnits)
Application ──► IConsumptionBillingService ──► linha de cobrança IA
```

- Entrada: dados do tenant, referências e prompt.
- Saída: `AiChangeSet` com snapshot proposto, nunca aplicado diretamente.
- Guardrails: validação de schema dos componentes e bloqueio de conteúdo inventado.
- Controle de custo: teto por requisição e por tenant.
- **Grau de personalização por plano:** o plano define o limite de personalização disponível (Essencial básica → Business total). A geração inicial e a publicação estão incluídas; **alterações posteriores ao lançamento geram `SiteChangeCharge`**, registrada antes de executar a alteração.
- A IA **não** gera código React, não altera arquivos do projeto e não modifica a estrutura da aplicação (diretrizes §6.1).

#### 5.3.1. Franquia mensal de IA

- Cada plano tem **franquia mensal própria de IA** (`Plan.IncludedAiUnits`).
- O consumo excedente à franquia é **cobrado adicionalmente**.
- A franquia de IA é **independente** da franquia de WhatsApp.
- O consumo é medido em `UsageQuota` com `Resource = AiUnits`.

### 5.4. Notificações

```text
Application ──► INotificationSender ──┬──► E-mail
                                     ├──► WhatsApp
                                     └──► SMS (opcional)
```

---

## 5A. Separação entre financeiro do tenant e billing da plataforma

Dois contextos financeiros **independentes** (diretrizes §8), com responsabilidades, regras e registros próprios.

| Contexto | Titular | Agregados | Não pode |
| --- | --- | --- | --- |
| **Financeiro do tenant** | O estabelecimento | `Payment`, `Transaction`, `CommissionRule`, `CommissionEntry` | Enxergar faturas da plataforma |
| **Billing da plataforma** | O Servia | `Subscription`, `PlatformInvoice`, `SiteChangeCharge`, `UsageQuota` | Enxergar receitas do estabelecimento |

**Regras de independência:**

- Cada contexto tem seus próprios agregados, tabelas e relatórios.
- Nenhum agregado de um contexto referencia o outro em suas regras de negócio.
- Ambos podem usar o mesmo `IPaymentGateway`, mas com contratos e registros separados.
- Uma falha em um contexto não corrompe o outro.
- A conciliação financeira (U56a) pertence ao financeiro do tenant; a conciliação de assinaturas pertence ao billing.

### 5A.1. Linhas de cobrança separadas

O billing emite **linhas distintas por origem**, garantindo transparência e controle de gastos:

```text
PlatformInvoice
  ├── Assinatura        (mensalidade do plano)
  ├── WhatsApp          (consumo tarifado ou créditos antecipados)
  ├── IA                (consumo acima da franquia mensal)
  └── Alterações de site (SiteChangeCharge)
```

| Linha | Base de cálculo | Apuração |
| --- | --- | --- |
| Assinatura | Mensalidade fixa do plano | `Subscription` |
| WhatsApp | Consumo acima da franquia gratuita da Meta, ou créditos | `UsageQuota(WhatsAppMessages)` |
| IA | Consumo acima da franquia do plano | `UsageQuota(AiUnits)` |
| Alterações de site | Rodadas de alteração após publicação | `SiteChangeCharge` |

**Regras:**

- WhatsApp e IA são **contabilizados e cobrados separadamente** — um recurso nunca encobre o outro na fatura.
- Cada linha é rastreável até o consumo que a originou.
- O tenant visualiza o detalhamento por recurso e controla seus gastos.

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
| `ScheduleLocks` | `(TenantId, ProfessionalId, Date, StartTime)` único parcial | Impede reserva concorrente do mesmo horário |
| `UsageQuotas` | `(TenantId, Resource, PeriodStart)` único | Consumo por tenant e período |
| `Customers` | `(TenantId, Id)` único | Alvo de FK composta com `TenantId` |
| `Professionals` | `(TenantId, Id)` único | Alvo de FK composta com `TenantId` |

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
| Segurança | Isolamento entre tenants | Teste dedicado que falha se o filtro for removido; testes de leitura, escrita e exclusão cross-tenant |
| Concorrência | Reservas simultâneas do mesmo horário | Teste que dispara N reservas paralelas e exige exatamente 1 sucesso |
| Integrações | Falha, indisponibilidade, repetição e resposta inesperada | Testes de contrato com dublês que simulam falha e timeout |
| Migração | Preservação dos dados existentes | Contagem e amostragem comparadas antes e depois da migração |
| Frontend | Fluxos principais de utilização | Playwright nos fluxos de cadastro, agendamento e cobrança |

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
├── src/
│   ├── Servia.Api/
│   ├── Servia.Application/
│   ├── Servia.Domain/
│   ├── Servia.Infrastructure/
│   └── Servia.Tests/
├── frontend/                  # ainda não construído
│   ├── src/
│   │   ├── routes/            # site público, agendamento, painel, admin
│   │   ├── components/site/   # biblioteca de componentes do site
│   │   └── services/          # cliente de API
├── docs/
│   ├── descricao_produto.md
│   ├── levantamento_lacunas.md   # inventário do código acadêmico (registro histórico)
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
| R9 | Reserva duplicada sob concorrência | Crítico | `ScheduleLock` com índice único parcial + transação + teste de concorrência |
| R10 | Consumo de WhatsApp acima do limite | Alto | `IUsageQuotaService` bloqueia no servidor antes do envio; `TenantLimit` próprio |
| R13 | Aprovação como Solution Partner não concedida | Crítico | Iniciar o processo na F0; F6 não entra em produção sem aprovação |
| R14 | Linha de crédito insuficiente para pagar a Meta | Alto | Monitorar consumo agregado; alerta antes de atingir o limite de crédito |
| R15 | Subsídio acidental de mensagens com custo | Alto | Franquia gratuita da Meta repassada explicitamente; teste que falha se mensagem tarifada for incluída sem cobrança |
| R16 | Custo de IA fora da franquia sem aviso | Médio | Aviso antes de consumir excedente; detalhamento por recurso na fatura |
| R11 | Núcleo contaminado por condicionais de segmento | Alto | Políticas injetadas; revisão rejeita `if` de segmento no núcleo |
| R12 | Mistura entre financeiro do tenant e billing | Alto | Contextos separados, sem referência cruzada entre agregados |

---

## 13. Decisões confirmadas

Todas as decisões estruturais foram confirmadas pelo responsável do produto em 2026-11-04.

| # | Decisão | Escolha confirmada | Impacto |
| --- | --- | --- | --- |
| D1 | Relação com o código acadêmico (ADR-001) | ✅ Sistema novo; código acadêmico não é reaproveitado | Fase F1 |
| D2 | Banco único com `TenantId` (ADR-002) | ✅ Banco único | Modelo de dados |
| D3 | PostgreSQL em produção (ADR-003) | ✅ PostgreSQL | Fase F0 |
| D4 | Gateway de pagamento (ADR-005) | ✅ Somente Mercado Pago | Fase F4 |
| D5 | Cloud API da Meta (ADR-006) | ✅ Cloud API oficial | Fase F6 |
| D6 | Provedor de LLM e teto de custo (ADR-007) | ⏳ Definir na fase F8 (abstração `IAiSiteGenerator`) | Fase F8 |
| D7 | PaaS e provedor de hospedagem (ADR-011) | ✅ Docker + GitHub Actions + PaaS | Fase F0 |
| D8 | Modelo LGPD controlador/operadora (ADR-012) | ✅ Tenant controlador, plataforma operadora | Fase F9 |
| D9 | Modelo de planos e regra de cobrança | ✅ 4 planos por faixa de profissionais; excedente exige upgrade | Fase F4 |
| D10 | Domínio próprio no MVP | ✅ Apenas subdomínio no MVP | Fase F3 |
| D11 | Identidade dos usuários (ADR-015) | ✅ Cadastros independentes por tenant, sem identidade global | Fase F1 |
| D12 | Isolamento em todas as camadas (ADR-016) | ✅ API, aplicação, domínio e persistência, com FK composta | Fase F1 |
| D13 | Concorrência no agendamento (ADR-017) | ✅ Trava de reserva com índice único parcial + transação | Fase F2 |
| D14 | Núcleo universal por políticas (ADR-018) | ✅ Políticas injetadas, sem condicional de segmento | Fases F2–F7 |
| D15 | Operação e cobrança do WhatsApp (ADR-019) | ✅ Solution Partner; consumo tarifado sem subsídio; automático ou créditos | Fase F6 |
| D17 | Franquia mensal de IA (ADR-021) | ✅ Franquia própria por plano, excedente cobrado à parte | Fase F8 |
| D18 | Cobrança separada de WhatsApp e IA (ADR-021) | ✅ Linhas distintas na fatura, com detalhamento por recurso | Fases F4–F6 |
| D16 | Separação financeiro × billing (ADR-020) | ✅ Domínios independentes | Fases F4–F5 |

### 13.1. Modelo de planos confirmado

**Todos os planos oferecem todos os serviços e módulos.** A diferenciação ocorre por quantidade de profissionais, grau de personalização do site e **franquia mensal de IA**.

| Plano | Profissionais incluídos | Módulos | Personalização do site | Franquia de IA |
| --- | --- | --- | --- | --- |
| Essencial | até 3 | Todos | Básica | faixa base |
| Pro | até 7 | Todos | Intermediária | faixa intermediária |
| Premium | até 15 | Todos | Avançada | faixa ampliada |
| Business | ilimitado | Todos | Total | faixa máxima |

**Regra de profissionais:** não há taxa por excedente. Ao ultrapassar o limite do plano, o tenant precisa subir de categoria.

**Regra de site:** a geração inicial por IA respeita o limite de personalização do plano. **Alterações posteriores ao lançamento são cobradas à parte**, independentemente do plano.

**Regra de WhatsApp:** o consumo é **tarifado e sem subsídio**. A franquia gratuita da Meta é repassada ao tenant; o excedente é cobrado conforme a modalidade escolhida (automática ou créditos antecipados).

**Regra de IA:** a franquia mensal é própria do plano; o excedente é cobrado adicionalmente.

> **Franquia não é bloqueio de módulo:** todos os planos têm acesso a WhatsApp e IA. O que varia é a quantidade incluída antes de passar a cobrar consumo.

### 13.2. Regra de trial e inadimplência confirmada

1. Trial de **14 dias** com cartão de crédito cadastrado.
2. Ao fim do trial, o cartão é debitado automaticamente.
3. Inadimplência: **3 dias de aviso com acesso total**.
4. Depois: **bloqueio de escrita**, com leitura e agenda disponíveis por **15 dias**.
5. Após 15 dias de bloqueio: suspensão total do tenant.

### 13.3. Pendências remanescentes

| # | Pendência | Quando resolver |
| --- | --- | --- |
| D6 | Provedor de LLM e teto de custo por geração | Fase F8, após comparar custo real |
| PD3 | Valores das franquias de WhatsApp e de IA por plano | Antes da fase F4 |
| PD4 | Tarifa de repasse do consumo de WhatsApp ao tenant | Antes da fase F6 |

> **PD1 encerrada em 2026-11-10:** a política comercial de WhatsApp e IA foi definida (ADR-019 e ADR-021).

### 13.4. Dependências externas

| # | Dependência | Impacto se não atendida |
| --- | --- | --- |
| DE1 | Aprovação do Servia como **Solution Partner** da Meta | Fase F6 não entra em produção |
| DE2 | **Linha de crédito** junto à Meta | Consumo de WhatsApp inviável em escala |
