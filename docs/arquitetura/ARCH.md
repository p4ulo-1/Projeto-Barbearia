# Arquitetura — Servia

> **Finalidade:** arquitetura de solução da plataforma multi-tenant, distinguindo o que está implementado, o que está aprovado e o que está planejado.
> **Escopo:** camadas, isolamento, persistência, concorrência, integrações, segurança, observabilidade, testes e dívida arquitetural.
> **Decisões:** ver [`ADR.md`](ADR.md). **Modelo de classes:** ver [`classes.md`](classes.md). **Catálogo de funcionalidades:** ver [`../produto/funcionalidades.md`](../produto/funcionalidades.md).
> **Base:** código em `src/`, verificado em 2026-11-14.

---

## 0. Estado da arquitetura

> **Leitura obrigatória:** este documento distingue **implementado**, **aprovado** e **planejado**. Uma seção marcada como "planejado" **não** descreve o que existe hoje.

### 0.1. Números reais (2026-11-14)

| Métrica | Valor |
| --- | --- |
| Entidades `ITenantScoped` no domínio | **46** |
| Contextos delimitados | 17 (`Ai`, `Appointments`, `Auditing`, `Billing`, `Catalog`, `Common`, `Customers`, `Finance`, `Identity`, `Loyalty`, `Memberships`, `Messaging`, `Operations`, `Policies`, `Sites`, `Team`, `Tenancy`) |
| Migrations | **14** |
| Tabelas no snapshot | **50** |
| Configurações EF | **21** |
| Controllers | **20** |
| Serviços de aplicação | **8** |
| Rotinas de job | **7** |
| Atributos `[Fact]`/`[Theory]` | **1011** |
| Controllers com acesso direto a `AppDbContext` | **13 de 20** |

### 0.2. Camadas

| Camada | Projeto | Estado |
| --- | --- | --- |
| Domínio | `Servia.Domain` | ✅ Implementado — entidades, objetos de valor, políticas, interfaces |
| Aplicação | `Servia.Application` | ✅ Implementado — 8 serviços, sem dependência de EF Core |
| Infraestrutura | `Servia.Infrastructure` | ✅ Implementado — EF Core 10 + Npgsql, repositórios, integrações |
| API | `Servia.Api` | ✅ Implementado — 20 controllers, middleware, autenticação, autorização |
| Testes | `Servia.Tests` | ✅ Implementado — 1011 atributos, PostgreSQL 17 via Testcontainers |
| Frontend | `web/` | ⚠️ Existente, **será substituído** — React 19 + React Router 7 + Vite 6 |

### 0.3. Isolamento multi-tenant — três camadas

| Camada | Mecanismo | Onde |
| --- | --- | --- |
| 1. Aplicação | Filtro global do EF Core | `AppDbContext.ApplyTenantFilter` |
| 2. Banco | Chaves estrangeiras compostas `(TenantId, Id)` | `CompositeForeignKeyConfiguration` |
| 3. Testes | Suítes cross-tenant | `Servia.Tests/Integration/` e `Servia.Tests/Persistence/` |

**Nenhuma das três é suficiente sozinha.** O filtro global não protege escrita; a FK não protege leitura; os testes provam que as duas funcionam juntas.

**Fonte do tenant:** exclusivamente a claim `tenant_id` do JWT, assinada pelo servidor. O header `X-Tenant-Id` foi **removido** e há teste que garante que não é aceito (`TenantResolutionTests`).

### 0.4. Persistência

- **PostgreSQL 17** é o único provider — desenvolvimento, testes e produção (ADR-022).
- Migrations versionadas (`MigrateAsync`), nunca `EnsureCreated`.
- **14 migrations**, snapshot alinhado, verificado por `MigrationTests` (12 testes).

### 0.5. Concorrência

| Mecanismo | Onde | Estado |
| --- | --- | --- |
| Transação `Serializable` | `AppointmentRepository.ExecuteSerializableAsync` | ✅ Implementado |
| `ScheduleLock` com índice único parcial | `ScheduleLock`, `PartialScheduleLockIndex` | ✅ Implementado |
| Token otimista `Site.Version` | `RemainingConfigurations.cs:220-221` | ✅ Implementado |
| `ConcurrencyConflictException` | `Domain/Common/` | ✅ Implementado |
| Lock distribuído para jobs | — | ❌ **Não implementado** |

### 0.6. O que está implementado, aprovado e planejado

| Área | Implementado | Aprovado, não implementado | Planejado |
| --- | --- | --- | --- |
| Autenticação | JWT 15 min, refresh com rotação, revogação | Cookie `HttpOnly`, CSRF, CORS | — |
| Autorização | 6 papéis, 17 permissões, `CustomRole`, claim `perm` | — | — |
| Billing | Contratar, trocar, cancelar, consultar, histórico | Cobrança no gateway, fatura, inadimplência | — |
| IA | Franquia, bloqueio, consumo em tokens | Compra de créditos, aquisição automática, notificação | — |
| Site | Criação, seções, preview, publish, rollback | Cobrança de alteração pós-publicação | — |
| Onboarding | Conversa, geração, ajustes, aprovação, publicação | — | — |
| Sites públicos | Resolução por host, throttle, agendamento | — | — |
| Jobs | 7 rotinas, executor único | Lock distribuído, retenção LGPD | — |
| Observabilidade | Health checks | Logs estruturados, métricas, trace, alertas | — |
| WhatsApp | — | — | Integração futura (pausada) |

---

## 1. Visão geral

A plataforma é um SaaS multi-tenant que atende negócios de serviços agendados. Cada estabelecimento opera em seu próprio ambiente isolado, com site individual, agenda, equipe, catálogo, financeiro, comunicação e billing da plataforma.

### 1.1. Princípios arquiteturais

| # | Princípio | Aplicação |
| --- | --- | --- |
| A1 | **Isolamento por tenant** | Todo dado de negócio carrega `TenantId`; isolamento em todas as camadas |
| A2 | **Modularidade por contexto** | 17 contextos delimitados com fronteiras claras |
| A3 | **Configuração em vez de bifurcação** | Diferenças entre segmentos por configuração, módulos e políticas injetadas |
| A4 | **Integrações atrás de abstrações** | Gateways, mensageria e LLM acessados por interfaces |
| A5 | **Aprovação humana em ações de IA** | Nenhuma alteração gerada por IA é publicada sem aprovação |
| A6 | **Segurança por padrão** | Falha fechada em fronteiras de confiança; autorização no servidor |
| A7 | **Observabilidade desde o início** | ⚠️ Parcial — só health checks |
| A8 | **Núcleo universal** | Funcionalidades compartilhadas num núcleo único |
| A9 | **Contextos financeiros independentes** | Financeiro do tenant e billing da plataforma separados |
| A10 | **Consistência sob concorrência** | Reserva garantida no banco, não só por consulta prévia |

---

## 2. Arquitetura de camadas

```text
┌──────────────────────────────────────────────────────────────┐
│  Servia.Api (entrada HTTP)                                   │
│  Controllers · Middleware · Auth · TenantResolution          │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Servia.Application (casos de uso)                           │
│  8 serviços · DTOs · Orquestração de integrações             │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Servia.Domain (regras de negócio)                           │
│  Entidades · Agregados · Objetos de valor · Interfaces        │
└───────────────────────────┬──────────────────────────────────┘
                            │
┌───────────────────────────▼──────────────────────────────────┐
│  Servia.Infrastructure (implementação)                       │
│  EF Core · Repositórios · Gateways · Mensageria · IA · JWT    │
└───────────────────────────┬──────────────────────────────────┘
                            │
                    ┌───────▼────────┐
                    │  PostgreSQL 17 │
                    └────────────────┘
```

### 2.1. Responsabilidades por camada

| Camada | Responsabilidade | Não deve conter |
| --- | --- | --- |
| **Api** | Roteamento, autenticação, resolução de tenant, tratamento de erro | Regra de negócio |
| **Application** | Casos de uso, transações, orquestração de integrações | Regra de domínio, EF Core |
| **Domain** | Entidades, agregados, invariantes, interfaces de repositório | Dependência de EF Core ou HTTP |
| **Infrastructure** | Persistência, integrações externas, segurança | Regra de negócio |

### 2.2. Regra de dependência (verificada nos `.csproj`)

```text
Api → Application → Domain
Api → Infrastructure → Domain
Tests → todos
```

- `Servia.Domain` não referencia nenhum projeto.
- `Servia.Application` referencia apenas `Domain` — **sem EF Core** (verificado: 0 ocorrências de `Microsoft.EntityFrameworkCore`).
- `Servia.Infrastructure` referencia apenas `Domain` — **sem `Application`** (verificado: 0 ocorrências de `using Servia.Application`).

### 2.3. Serviços de aplicação

| Serviço | Responsabilidade |
| --- | --- |
| `AppointmentService` | Criação de agendamento com verificação de sobreposição |
| `AvailabilityService` | Disponibilidade do profissional e do estabelecimento |
| `AuthenticationService` | Sign-in, refresh com rotação, sign-out |
| `SubscriptionService` | Contratar, trocar, cancelar, sugerir upgrade |
| `AiUsageService` | Franquia, bloqueio e consumo de IA em tokens |
| `OnboardingService` | Onboarding conversacional com IA |
| `SiteAssetService` | Upload e gestão de assets do site |
| `WebhookService` | Pagamento e mensagem recebida, idempotência |

### 2.4. Dívida arquitetural

| # | Dívida | Evidência | Impacto |
| --- | --- | --- | --- |
| D1 | **13 de 20 controllers acessam `AppDbContext` diretamente** | `Agenda`, `Billing`, `Catalog`, `CustomRoles`, `Customers`, `Finance`, `Modules`, `OnboardingAssets`, `PublicSite`, `Site`, `Team`, `TenantSettings`, `WhatsApp` | Lógica de negócio nos controllers; difícil testar sem subir a API |
| D2 | **`AppDbContext` com 50 DbSets** | `AppDbContext.cs` (208 linhas) | Ponto único de acesso; dificulta modularização |
| D3 | **`PublicSiteController` orquestra domínio na API** | Busca serviço/profissional, cria cliente por telefone, chama `AppointmentService` | Extrair `PublicBookingService` para a Application |
| D4 | **Políticas de segmento não injetadas** | `IBookingPolicy`/`IAvailabilityPolicy` só em testes | Extensibilidade por segmento não exercida |
| D5 | **`LocalSiteAssetStorage` grava em disco local** | `Infrastructure/Storage/` | Não serve múltiplas instâncias |
| D6 | **`IIdempotencyStore` em memória** | `Infrastructure/Integrations/IdempotencyStore.cs` | 0 consumidores; resíduo |

---

## 3. Isolamento multi-tenant

### 3.1. Estratégia

Banco único com `TenantId` (ADR-002). O isolamento é garantido em três níveis:

1. **Resolução do tenant** — `TenantResolutionMiddleware` identifica o tenant **exclusivamente** pela claim `tenant_id` do JWT.
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

| Camada | Mecanismo |
| --- | --- |
| **API** | `TenantResolutionMiddleware` resolve o tenant da claim do JWT — nunca de valor livre do frontend |
| **Aplicação** | Serviços recebem `tenantId` explícito; repositórios filtram por ele |
| **Domínio** | Agregados recebem o `TenantId` do contexto |
| **Persistência** | Filtro global na leitura + verificação de `TenantId` na escrita + FK composta |

> **Nota:** o ARCH anterior citava `ITenantMembershipValidator` como camada de isolamento. **Essa interface não existe.** O isolamento real é o descrito acima.

### 3.3. Restrições de integridade no banco

Relacionamentos entre entidades de negócio usam **chave estrangeira composta incluindo `TenantId`**, de modo que o PostgreSQL rejeite a associação de um registro de um tenant a outro.

```text
Appointments (TenantId, CustomerId) ──► Customers (TenantId, Id)
Appointments (TenantId, ProfessionalId) ──► Professionals (TenantId, Id)
```

### 3.4. Identidade independente por tenant

Não existe identidade global de usuário (ADR-015). A mesma pessoa física que frequenta dois estabelecimentos possui **dois cadastros `User` independentes**. O e-mail é único **por tenant**. Administradores da plataforma são a única exceção: `TenantId = Guid.Empty` e papel `PlatformAdmin`.

### 3.5. Usos de `IgnoreQueryFilters` (lista autorizada)

| Local | Justificativa |
| --- | --- |
| `PublicSiteReader` | Requisição pública sem tenant; consulta restrita a sites publicados |
| `WebhookTenantResolver` | Webhook chega sem contexto; a busca descobre o tenant |
| `UserRepository.FindByEmailAsync` | Bypass restrito a `TenantId == Guid.Empty` + `PlatformAdmin` + e-mail exato |
| `JobRepository` | Rotinas processam todos os tenants; cada operação atua sobre o registro que já carrega seu `TenantId` |

**Regra:** `IgnoreQueryFilters()` fora desta lista é proibido. Um novo uso exige justificativa e teste.

### 3.6. Riscos e mitigação

| Risco | Mitigação |
| --- | --- |
| Vazamento entre tenants | Testes de isolamento obrigatórios; FK composta; revisão de `IgnoreQueryFilters` |
| Consulta cross-tenant acidental | Repositórios só expõem métodos que aplicam o filtro |
| Tenant forjado pelo cliente | Tenant resolvido no servidor; header ignorado |
| Bypass do platform admin expor dados de tenant | Restrito a `TenantId == Guid.Empty` + `PlatformAdmin` + e-mail exato |

---

## 4. Modelo de permissões e separação de papéis

### 4.1. Papéis

| Papel | Escopo | Descrição |
| --- | --- | --- |
| `PlatformAdmin` | Plataforma | Não pertence a um tenant; privilégios distintos das permissões de tenant |
| `Owner` | Tenant | Titular da conta; criado automaticamente; **todas** as permissões de tenant, incluindo `ManageBilling` |
| `Administrator` | Tenant | Administra a operação; **não** tem `ManageBilling` |
| `Receptionist` | Tenant | Agenda de qualquer profissional, clientes; **não** gerencia a equipe |
| `Professional` | Tenant | Apenas os próprios compromissos |
| `Customer` | Tenant | Cliente final |

### 4.2. Permissões (17 valores)

`ViewTenantSettings`, `ManageTenantSettings`, `ManageTeam`, `ManageBilling`, `ViewServices`, `ManageServices`, `ViewOwnAgenda`, `ViewTeamAgenda`, `ManageAppointments`, `CancelAnyAppointment`, `ViewCustomers`, `ManageCustomers`, `ViewFinancialReports`, `ManageFinancialEntries`, `ViewOwnCommissions`, `ManagePlatformTenants`, `ManagePlatformPlans`.

### 4.3. Mecanismo de autorização

- `[RequirePermission(Permission.X)]` gera a política `permission:X`.
- O token carrega a claim **`perm`** com as permissões efetivas (papel predefinido ou personalizado).
- `PermissionAuthorizationHandler` lê `perm`; se ausente, cai no `role` (compatibilidade com tokens antigos).
- `[RequirePlatformAdmin]` exige claim `role = PlatformAdmin`.

### 4.4. Papéis personalizados (`CustomRole`)

- Entidade `ITenantScoped` com nome único por tenant e conjunto de permissões.
- `User.CustomRoleId` opcional; `User.EffectivePermissions(customRole)`.
- `CustomRolesController` (`api/v1/team/roles`, `ManageTeam`): CRUD, `assignable-permissions`, atribuir/remover.
- **Bloqueio:** `ManageBilling` e permissões de plataforma são rejeitadas.
- **Anti-escalonamento:** `RolePermissions.EnsureCanGrant` impede conceder permissão que o ator não possui; auto-atribuição bloqueada.
- **Revogação:** alterar permissões, atribuir, remover ou excluir papel revoga os refresh tokens dos usuários afetados.

### 4.5. Papel × atuação profissional

`Professional.IsServiceProvider` (default `true`) + `OccupiesPlanSlot = IsActive && IsServiceProvider`. O perfil de acesso **não** define ocupação de vaga no plano.

---

## 5. Onboarding conversacional com IA

### 5.1. Fluxo

```text
start → mensagens → generate → adjustments → submit → approve → publish
```

| Estado (`OnboardingStatus`) | Significado |
| --- | --- |
| `Collecting` | Coleta de informações por conversa |
| `Reviewing` | Configuração gerada, em revisão |
| `AwaitingApproval` | Aguardando aprovação explícita |
| `Approved` | Aprovada, pronta para publicar |
| `Published` | Publicada |

### 5.2. Componentes

| Componente | Papel |
| --- | --- |
| `OnboardingSession` | Sessão com histórico persistido e fatos coletados |
| `OnboardingService` | Caso de uso: conversa, geração, ajustes, aprovação, publicação |
| `IAiConversationProvider` | Contrato do provedor de conversa (`OpenAiConversationProvider` / `UnconfiguredConversationProvider`) |
| `IAiSiteGenerator` | Contrato do gerador de site (`OpenAiSiteGenerator` / `UnconfiguredSiteGenerator`) |
| `AiChangeSet` | Proposta validada contra `SiteComponentSchema` |
| `AiSiteChange` + `SiteChangeApplier` | Alteração cirúrgica pós-publicação — **infraestrutura sem fluxo** |

### 5.3. Guardrails

- Componente ou propriedade fora do schema → rejeição.
- Proposta incompleta → recusada.
- Publicar sem aprovar → `400`.
- Provedor não configurado → `400` com motivo, nunca simulação.

### 5.4. Endpoints

`POST /api/v1/onboarding/start`, `GET /sessions`, `GET /sessions/{id}`, `GET/POST /sessions/{id}/messages`, `POST /sessions/{id}/generate`, `POST /sessions/{id}/adjustments`, `POST /sessions/{id}/submit`, `POST /sessions/{id}/approve`, `POST /sessions/{id}/publish`, `GET /sessions/{id}/preview`.

---

## 6. Sites públicos e resolução por host

### 6.1. Resolução

`PublicSiteReader.FindPublishedByHostAsync` compara o **primeiro rótulo** do host com `Site.Subdomain` e o host inteiro com `Site.CustomDomain`. Não fixa o domínio da plataforma no código.

### 6.2. Endpoints públicos

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/api/v1/public/site` (`?host=`) | Site publicado |
| GET | `/api/v1/public/site/services` | Serviços do tenant |
| GET | `/api/v1/public/site/professionals` | Profissionais |
| GET | `/api/v1/public/site/availability` | Disponibilidade |
| POST | `/api/v1/public/site/appointments` | Agendamento público |

### 6.3. Segurança

- Tenant resolvido pelo host, fixado no contexto **na ação** (não dentro de método `async`).
- `IgnoreQueryFilters` explícito e restrito a sites publicados.
- Throttle persistente no PostgreSQL (`PostgresPublicBookingThrottle`), atômico entre instâncias.
- `ClientOriginResolver` só aceita `X-Forwarded-For` de proxy confiável.

---

## 7. Política de consumo de IA por tokens

### 7.1. Regras

| Regra | Implementação |
| --- | --- |
| 1 crédito = 10.000 tokens | `UsageQuota.AiTokensPerCredit` |
| Acumulação pelo total real | `UsageQuota.AccumulateAiTokens` (soma tokens, converte sobre o total) |
| Franquia mensal por plano | `Plan.IncludedAiUnits` (100/500/1500/5000) |
| Bloqueio ao esgotar | `AiUsageService.EnsureAvailableAsync` |
| Renovação no novo período | `UsageQuota.Reset` |

### 7.2. Fluxo

```text
Operação de IA solicitada
        │
        ▼
EnsureAvailableAsync → HasAiCapacity?
        │ sim              │ não
        ▼                  ▼
   executa          DomainRuleException (bloqueia)
        │
        ▼
RecordConsumptionAsync (tokens reais)
```

### 7.3. Não implementado

- Notificação ao cliente ao esgotar.
- Compra manual de créditos (`CreditPurchase` existe, sem fluxo).
- Autorização de aquisição automática (entidade não existe).
- Cobrança de excedente (descartada por decisão de produto).

---

## 8. Expediente do estabelecimento

| Regra | Implementação |
| --- | --- |
| Configurável pelo administrador | `GET/PUT /api/v1/tenant/settings/schedule` |
| Horário por dia, dias podem estar fechados | `EstablishmentSchedule`, `EstablishmentDaySchedule` |
| Independe dos profissionais | `AvailabilityService` não consulta o estabelecimento para agendar |
| Sem configuração, fechado 24/7 | `EstablishmentSchedule.ClosedByDefault()` |

**Decisão (BR5):** o expediente descreve quando o estabelecimento atende ao público. **Não** determina nem restringe agendamentos, que dependem exclusivamente da jornada, dos bloqueios e da duração do serviço do profissional.

---

## 9. Integrações externas

### 9.1. Pagamentos

```text
Application ──► IPaymentGateway ──► MercadoPagoGateway
```

- **Gateway confirmado:** Mercado Pago (Pix, boleto e cartão).
- Contrato: registrar cliente, criar assinatura, criar cobrança, estornar, cancelar assinatura.
- **Estado:** `IPaymentGateway` e `MercadoPagoGateway` implementados e testados (`ProviderTests`, 16). **Nenhum caso de uso chama o gateway** — infraestrutura sem fluxo.

### 9.2. Mensageria (WhatsApp)

```text
Application ──► IMessagingProvider ──► MetaWhatsAppProvider
                                   └──► UnconfiguredMessagingProvider
```

- Contrato: enviar texto, enviar template.
- **Estado:** provedor implementado e testado. **Sem endpoint de envio pelo tenant.**
- **Decisão comercial (2026-11-14):** o estabelecimento é titular da própria conta WhatsApp Business Platform e paga diretamente à Meta. O Servia fornece integração, orientação e estimativas, mas **não intermedia a cobrança**. A integração está **pausada**.

### 9.3. IA

```text
Application ──► IAiSiteGenerator ──► OpenAiSiteGenerator
Application ──► IAiConversationProvider ──► OpenAiConversationProvider
```

- Modelo configurável (`gpt-4o-mini` em `appsettings.Production.json`).
- Sem credencial, o provedor não é registrado e o endpoint responde `400` com motivo.

### 9.4. Notificações

```text
Application ──► IMessagingProvider (WhatsApp)
```

- `Notification` existe com ciclo `Scheduled → Sent/Failed`.
- `NotificationDispatchJob` envia as vencidas (1 min, lote 100).
- **Estado:** `Notification.Create` tem 0 chamadores — a fila nunca é alimentada.
- **Canais:** `MessageChannel` tem `Email` e `Sms`, mas **nenhum provedor** os implementa.

---

## 10. Segurança

| Área | Decisão | Estado |
| --- | --- | --- |
| Senhas | PBKDF2-HMACSHA256, 600.000 iterações, salt por senha, `FixedTimeEquals` | ✅ Implementado |
| Rehash | Comparar iterações do hash com `DefaultIterations` no login | ❌ **Não implementado** |
| Sessão | JWT de acesso 15 min + refresh token opaco com rotação e revogação | ✅ Implementado |
| Refresh em cookie `HttpOnly` | — | ❌ **Não implementado** (token vai no corpo JSON) |
| CSRF | — | ❌ **Não implementado** |
| CORS | — | ❌ **Não implementado** |
| Rate limiting | Throttle no agendamento público e no signup | ⚠️ **Parcial** — sem limite no login |
| Autorização | Verificação por permissão no servidor | ✅ Implementado |
| Transporte | HTTPS obrigatório; HSTS em produção | ✅ Implementado |
| Segredos | Variáveis de ambiente; falha ao subir sem chave JWT em produção | ✅ Implementado |
| Auditoria | `AuditEntry` imutável | 🔧 **Infraestrutura sem fluxo** |
| Bloqueio de escrita por inadimplência | `Tenant.CanWrite` existe | ❌ **Não aplicado** |

---

## 11. Observabilidade e operação

| Item | Estado | Evidência |
| --- | --- | --- |
| Health checks | ✅ | `/health`, `/health/live`, `/health/ready`; só verifica o banco |
| Logs estruturados | ❌ | Formatter padrão em texto; sem `TenantId`/`UserId` |
| Métricas | ❌ | `OperationMetrics` sem chamadores |
| Trace | ⚠️ | `Activity.Current?.Id` só no erro 500 |
| Alertas | ❌ | `AlertRule` sem chamadores |
| Backup | ⚠️ | `scripts/backup.sh`; sem agendamento nem registro em `TenantBackup` |
| Restauração testada | ❌ | — |
| Runbook | ⚠️ | Existe; contém seção obsoleta (SQLite) |
| Pipeline | ⚠️ | Restore, build, test, Docker; sem lint, staging, smoke ou deploy |

---

## 12. Estratégia de testes

### 12.1. O que existe

| Nível | Ferramenta | Estado |
| --- | --- | --- |
| Domínio | xUnit | ✅ 56 arquivos por contexto |
| Aplicação | xUnit + dublês | ✅ `SubscriptionServiceTests` (17), `WebhookServiceTests` (13), `AiUsageServiceTests` (10), `AppointmentServiceTests`, `AuthenticationServiceTests` |
| Integração | xUnit + Testcontainers (PostgreSQL 17) | ✅ 16 arquivos em `Persistence/`, 9 em `Integration/` |
| Contrato | Dublês de HTTP | ✅ `ProviderTests` (16) |
| Segurança | Isolamento cross-tenant | ✅ 5 suítes (33 testes) |
| Concorrência | Reservas simultâneas | ✅ `AppointmentConcurrencyTests` (8), `SiteConcurrencyPersistenceTests` (2) |
| Migração | Schema, índices, seed | ✅ `MigrationTests` (12) |
| Frontend | Vitest | ⚠️ 2 arquivos; frontend será substituído |

### 12.2. O que não existe

| Lacuna | Impacto |
| --- | --- |
| E2E | Nenhum teste ponta a ponta do fluxo de negócio |
| Teste HTTP de webhook | Assinatura, parsing e 401 não cobertos |
| Teste HTTP de escrita de billing | `POST/PATCH/cancel` só no serviço |
| Teste de bloqueio por inadimplência | A regra não é aplicada |
| Teste de CSRF/cookie | Não há o que testar |
| Relatório de cobertura no CI | `coverlet.collector` presente, sem relatório |
| Definição de Concluído | Não existe |

---

## 13. Estrutura real do frontend

> **O frontend atual será substituído.** Esta seção registra o que existe, não o que será.

| Item | Realidade |
| --- | --- |
| Stack | React 19 + React Router 7 + Vite 6 + TypeScript 5.7 |
| Rotas | `/`, `/criar-conta`, `/site`, `/agendar`, `/entrar`, `/painel`, `/painel/servicos`, `/painel/equipe`, `/painel/clientes`, `/painel/financeiro`, `/painel/site`, `/painel/assinatura` |
| Páginas | `pages/index.tsx`, `catalog.tsx`, `management.tsx`, `operations.tsx`, `public.tsx` |
| Cliente de API | `api/client.ts` — token em memória, refresh em `localStorage` |
| Testes | Vitest: `client.test.ts` (8), `forms.test.ts` (4) |
| Design system | Dois sistemas paralelos: `styles.css` (painel) e `landing/tokens.css` (landing) |
| Navegação por perfil | Links fixos; não consulta `Role`/`Permission` |

> **Divergências com o ARCH anterior:** TanStack, Tailwind e shadcn **não** são usados; o refresh token **não** está em cookie `HttpOnly`; os arquivos `agendamento.tsx`, `auth.tsx`, `conta.tsx` e `admin.tsx` **não existem**.

---

## 14. Aspectos relevantes da API

### 14.1. Convenções

- Base `/api`; rotas de negócio em `/api/v1/...`; autenticação em `/api/auth`.
- Erro único: `ErrorResponse { message, traceId? }`.
- Paginação: `PagedResponse<T>` com `PageRequest.Normalize`.
- `DomainRuleException` → `400`; `NotFoundException` → `404`; inesperada → `500` com `traceId`.

### 14.2. Controllers (20)

| Controller | Rota base | Auth |
| --- | --- | --- |
| `AuthController` | `api/auth` | anônimo |
| `PublicSignupController` | `api/v1/public` | anônimo + throttle |
| `PublicSiteController` | `api/v1/public/site` | anônimo + throttle |
| `WebhooksController` | `api/v1/webhooks` | anônimo + HMAC |
| `AgendaController` | `api/v1/agenda` | JWT + permissão |
| `AppointmentsController` | `api/appointments` | JWT + permissão (legado) |
| `CatalogController` | `api/v1/catalog` | JWT + permissão |
| `TeamController` | `api/v1/team` | JWT + permissão |
| `CustomRolesController` | `api/v1/team/roles` | JWT + `ManageTeam` |
| `CustomersController` | `api/v1/customers` | JWT + permissão |
| `FinanceController` | `api/v1/finance` | JWT + permissão |
| `BillingController` | `api/v1/billing` | JWT + permissão |
| `ModulesController` | `api/v1/modules` | JWT + permissão |
| `WhatsAppController` | `api/v1/whatsapp` | JWT + permissão |
| `SiteController` | `api/v1/site` | JWT + permissão |
| `OnboardingController` | `api/v1/onboarding` | JWT + permissão |
| `OnboardingAssetsController` | `api/v1/onboarding/.../assets` | JWT + permissão |
| `TenantSettingsController` | `api/v1/tenant/settings` | JWT + permissão |
| `PlatformTenantsController` | `api/v1/platform/tenants` | JWT + `PlatformAdmin` |
| `DiagnosticsController` | `api/diagnostics` | — |

### 14.3. Saúde

`/health` (API + banco), `/health/live` (processo), `/health/ready` (API + banco).

---

## 15. Riscos arquiteturais

| # | Risco | Impacto | Mitigação |
| --- | --- | --- | --- |
| R1 | Vazamento entre tenants | Crítico | Filtro global + FK composta + testes de isolamento |
| R2 | Custo de LLM fora de controle | Alto | Franquia + bloqueio; **sem teto por requisição** |
| R3 | Aprovação da Meta atrasar a integração | Médio | Integração pausada; não bloqueia o produto |
| R4 | Reserva duplicada sob concorrência | Crítico | `ScheduleLock` + transação + teste |
| R5 | Jobs duplicados em múltiplas instâncias | Alto | **Não mitigado** — sem lock distribuído |
| R6 | Webhooks duplicados ou fora de ordem | Alto | `ProcessedEvent` com índice único |
| R7 | Dependência de provedor único de IA | Médio | Interfaces de abstração |
| R8 | Núcleo contaminado por condicionais de segmento | Alto | Políticas injetadas (não exercidas no fluxo) |
| R9 | Mistura entre financeiro do tenant e billing | Alto | Contextos separados |
| R10 | Access token válido por 15 min após mudança de permissão | Médio | Revogação de refresh; risco residual aceito |

---

## 16. Decisões confirmadas

Ver [`ADR.md`](ADR.md) para o registro completo (ADR-001 a ADR-027).

| # | Decisão | ADR |
| --- | --- | --- |
| D1 | Sistema novo, sem reaproveitar código acadêmico | ADR-001 |
| D2 | Banco único com `TenantId` | ADR-002 |
| D3 | PostgreSQL 17 em todos os ambientes | ADR-003, ADR-022 |
| D4 | Somente Mercado Pago | ADR-005 |
| D5 | WhatsApp via Cloud API da Meta | ADR-006 |
| D6 | Site gerado por IA sobre biblioteca de componentes | ADR-007 |
| D7 | Aprovação obrigatória de alterações de IA | ADR-008 |
| D8 | `User` separado de `Customer` | ADR-009 |
| D9 | `Professional` generaliza `Barber` | ADR-010 |
| D10 | Docker + GitHub Actions + PaaS | ADR-011 |
| D11 | LGPD: tenant controlador, plataforma operadora | ADR-012 |
| D12 | Auditoria imutável por inserção | ADR-013 |
| D13 | Objeto de valor `Money` | ADR-014 |
| D14 | Identidade independente por tenant | ADR-015 |
| D15 | Isolamento em todas as camadas | ADR-016 |
| D16 | Concorrência na reserva | ADR-017 |
| D17 | Núcleo universal com políticas | ADR-018 |
| D18 | Operação do WhatsApp como Solution Partner | ADR-019 — **superado** pela decisão de 2026-11-14 |
| D19 | Separação financeiro × billing | ADR-020 |
| D20 | Franquia mensal de IA | ADR-021 — **revisado** por ADR-024 |
| D21 | WhatsApp: estabelecimento titular da conta, pagamento direto à Meta | ADR-023 |
| D22 | Consumo de IA por tokens: 1 crédito = 10.000 tokens | ADR-024 |
| D23 | Owner × Administrator, papéis personalizados, claim `perm` | ADR-025 |
| D24 | Revogação de tokens e anti-escalonamento | ADR-026 |
| D25 | Cobrança de alterações de site após a publicação | ADR-027 |

---

## 17. Pendências arquiteturais

| # | Pendência | Origem |
| --- | --- | --- |
| P1 | Ciclo financeiro do billing (gateway, fatura, inadimplência) | Auditoria ARCH |
| P2 | Cookie `HttpOnly` + CSRF + CORS | Decisão obrigatória |
| P3 | Observabilidade (logs, métricas, trace, alertas) | Auditoria ARCH |
| P4 | Lock distribuído para jobs | Auditoria ARCH |
| P5 | Bloqueio de escrita por inadimplência | Auditoria ARCH |
| P6 | Idempotência de operações financeiras | Auditoria ARCH |
| P7 | Rehash de senha | Auditoria ARCH |
| P8 | Rate limiting no login | Auditoria ARCH |
| P9 | Auditoria de ações sensíveis | Auditoria ARCH |
| P10 | Redução do `AppDbContext` nos controllers | Dívida arquitetural |
| P11 | Injeção das políticas de segmento | Dívida arquitetural |
| P12 | Integração com WhatsApp Business Platform | Pausada, condicionada à Meta |