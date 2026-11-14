# Funcionalidades — Catálogo do Servia

> **Finalidade:** catálogo central das funcionalidades do Servia, com o estado real de cada uma.
> **Fonte de verdade do estado:** código em `src/`, verificado em 2026-11-14.
> **Regra de leitura:** uma funcionalidade só é marcada como implementada quando o **fluxo funcional completo** existe — entrada (endpoint/job/evento) → regra → persistência → efeito observável. A existência isolada de entidade, interface ou endpoint **não** comprova implementação.
> **Documentos relacionados:** visão e princípios em [`descricao_produto.md`](descricao_produto.md); arquitetura em [`../arquitetura/ARCH.md`](../arquitetura/ARCH.md); decisões em [`../arquitetura/ADR.md`](../arquitetura/ADR.md).

---

## 0. Premissas estruturais e regras comerciais

### 0.1. Planos comerciais

**Todos os planos oferecem todos os serviços e módulos.** A diferenciação está na quantidade de profissionais incluídos, no grau de personalização do site e na franquia mensal de IA.

| Plano | Profissionais incluídos | Personalização do site | Franquia de IA/mês |
| --- | --- | --- | --- |
| Essencial | até 3 | Básica | 100 créditos (1 milhão de tokens) |
| Pro | até 7 | Intermediária | 500 créditos (5 milhões de tokens) |
| Premium | até 15 | Avançada | 1.500 créditos (15 milhões de tokens) |
| Business | ilimitado | Total | 5.000 créditos (50 milhões de tokens) |

> **Fonte única dos valores:** `src/Servia.Infrastructure/Persistence/PlanSeeder.cs`. Em caso de divergência entre este documento e o código, **prevalece o código**. Os demais documentos apontam para esta seção.

**Regra de profissionais:** não há taxa por profissional excedente. Ao ultrapassar o limite, o tenant precisa **subir de categoria**. A contagem considera apenas quem **presta serviços** (`IsActive && IsServiceProvider`), independentemente do papel de acesso.

**Regra de módulos:** franquia de consumo **não** é bloqueio de módulo. Nenhum módulo é desabilitado por plano.

### 0.2. Consumo de IA

| Regra | Situação |
| --- | --- |
| 1 crédito = 10.000 tokens (entrada + saída) | **Implementado** (`UsageQuota.AiTokensPerCredit`) |
| Consumo acumulado pelo total real, sem arredondar por operação | **Implementado** (`AccumulateAiTokens`) |
| Franquia mensal por plano | **Implementado** (`Plan.IncludedAiUnits`) |
| Ao esgotar, bloquear novas operações | **Implementado** (`AiUsageService.EnsureAvailableAsync`) |
| Notificar o cliente ao esgotar | **Não implementado** |
| Compra manual de créditos adicionais | **Não implementado** (`CreditPurchase` existe, sem fluxo) |
| Autorização prévia de aquisição automática | **Não implementado** (não existe entidade) |
| Cobrança ilimitada por token excedente | **Descartado** por decisão de produto |

### 0.3. Alterações de site após a publicação

**Tabela aprovada:**

| Tipo de alteração | Preço |
| --- | --- |
| Ajuste rápido | R$ 39 |
| Seção simples | R$ 79 |
| Nova seção | R$ 149 |
| Página simples | R$ 249 |
| Reformulação de página | R$ 349 |
| Reformulação ampla | A partir de R$ 499 |
| Fora da tabela | R$ 120/hora, em blocos de 30 min, com orçamento e aprovação prévios |

**Regras complementares:** o cliente conhece escopo, preço e prazo antes da execução; nenhuma alteração paga começa sem aprovação; cada solicitação inclui uma rodada de correção; mudança de ideia ou pedido adicional é nova solicitação; pequenas alterações podem ser agrupadas; defeitos do Servia e alterações feitas pelo próprio cliente não são cobrados; a tabela é configurável; a classificação considera complexidade e escopo real.

**Situação:** **não implementado.** `SiteChangeCharge` existe como entidade, mas nenhum fluxo cria a cobrança. A tabela de preços não existe no código.

### 0.4. WhatsApp

**Decisão aprovada (2026-11-14):** o estabelecimento é titular da própria conta WhatsApp Business Platform e **paga diretamente à Meta**. O Servia fornece integração, orientação e estimativas de custo, mas **não intermedia a cobrança**, não vende créditos e não mantém carteira ou franquia.

**Situação:** integração **pausada**, condicionada à viabilidade e aos requisitos de acesso da Meta. Não é atividade de implementação imediata.

### 0.5. Legenda de estado

| Símbolo | Significado |
| --- | --- |
| ✅ | **Implementado** — fluxo completo e testado |
| ⚠️ | **Parcialmente implementado** — parte do fluxo existe |
| 🔧 | **Infraestrutura sem fluxo** — entidade/interface existe, nenhum caminho de produção a usa |
| 📋 | **Planejado** — aprovado, não implementado |
| ❌ | **Não implementado** |
| 🚫 | **Descartado / fora de escopo** |

---

## 1. Funcionalidades universais

### 1.1. Conta, autenticação e acesso

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U01 | Cadastro de estabelecimento (tenant) | ✅ | `POST /api/v1/public/signup` (`PublicSignupController`), `POST /api/v1/platform/tenants` (`PlatformTenantsController`), `TenantProvisioner` |
| U02 | Autenticação de usuários | ✅ | `POST /api/auth/sign-in`, `JwtTokenIssuer` |
| U03 | Registro de usuários vinculados ao tenant | ✅ | `TenantProvisioner` cria o Owner; `CustomRolesController` gerencia papéis |
| U04 | Recuperação de senha | ❌ | Nenhum endpoint de recuperação |
| U05 | Perfil do usuário | ⚠️ | `User.UpdateProfile` existe; sem endpoint dedicado |
| U06 | Alteração de senha | ⚠️ | `User.ChangePassword` existe; sem endpoint dedicado |
| U07 | Exclusão de conta | ❌ | Sem endpoint |
| U08 | Papéis e permissões | ✅ | 6 papéis, 17 permissões, `RolePermissions`, `RequirePermissionAttribute` |
| U08a | Identidade independente por tenant | ✅ | `User` é `ITenantScoped`; e-mail único por tenant |
| U08b | Papéis personalizados (`CustomRole`) | ✅ | `CustomRolesController` (7 endpoints), claim `perm`, anti-escalonamento |
| U09 | Convite de membros da equipe | ❌ | `Invitation` existe como entidade; sem endpoint |
| U10 | Sessão e renovação de token | ✅ | `POST /api/auth/refresh` com rotação, `POST /api/auth/sign-out` com revogação |
| U10a | Revogação de tokens ao alterar permissões | ✅ | `IRefreshTokenRepository.RevokeAllForUserAsync` em `CustomRolesController` |

### 1.2. Gestão de clientes

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U11 | Cadastro de clientes | ✅ | `CustomersController` (list, get, create) |
| U12 | Histórico do cliente | ⚠️ | Dados existem (agendamentos, pagamentos); sem consulta consolidada |
| U13 | Preferências do cliente | ✅ | `GET/PUT /api/v1/customers/{id}/preferences` |
| U14 | Busca e segmentação | ⚠️ | Busca por nome/telefone; sem filtro por última visita ou status |
| U15 | Consentimento LGPD | ❌ | Não existe `ConsentRecord` |
| U16 | Anonimização e exclusão de dados | ✅ | `POST /api/v1/customers/{id}/anonymize` |

### 1.3. Serviços e catálogo

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U17 | Cadastro de serviços | ✅ | `POST /api/v1/catalog/services` |
| U18 | Ativação e desativação de serviços | ✅ | `POST /api/v1/catalog/services/{id}/deactivate` |
| U19 | Categorias de serviços | ✅ | `GET/POST /api/v1/catalog/categories` |
| U20 | Variações de serviço | 🔧 | `ServiceVariant` existe; sem endpoint |
| U21 | Combos de serviços | ✅ | `GET/POST /api/v1/catalog/combos` |

### 1.4. Profissionais e equipe

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U22 | Cadastro de profissionais | ✅ | `POST /api/v1/team/professionals` |
| U23 | Jornada de trabalho | ✅ | `WorkSchedule` no cadastro do profissional |
| U24 | Bloqueios e folgas | ✅ | `POST /api/v1/team/professionals/{id}/blocks` |
| U25 | Vínculo profissional ↔ serviço | ✅ | `POST/GET /api/v1/team/professionals/{id}/services` |
| U26 | Agenda por profissional | ✅ | `GET /api/v1/agenda/appointments` com escopo por profissional |
| U26a | Atuação como prestador independente do papel | ✅ | `Professional.IsServiceProvider` + `PATCH /professionals/{id}/service-provider` |

### 1.5. Agendamento e disponibilidade

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U27 | Consulta de disponibilidade | ✅ | `GET /api/v1/agenda/availability`, `AvailabilityService` |
| U28 | Criação de agendamento | ✅ | `POST /api/v1/agenda/appointments` |
| U28a | Reserva sob concorrência | ✅ | Transação `Serializable` + `ScheduleLock` com índice único parcial |
| U29 | Remarcação | ✅ | `PATCH /api/v1/agenda/appointments/{id}/reschedule` |
| U30 | Cancelamento | ✅ | `POST /api/v1/agenda/appointments/{id}/cancel` |
| U31 | Conclusão de atendimento | ✅ | `POST /api/v1/agenda/appointments/{id}/complete` |
| U32 | Listagem de agendamentos | ✅ | `GET /api/v1/agenda/appointments` |
| U33 | Regras de antecedência | 🔧 | `IBookingPolicy.MinimumLeadTimeMinutes` existe; não injetada no fluxo |
| U34 | Fila de espera | 🔧 | `WaitlistEntry` existe; sem endpoint |
| U35 | Sinal para reserva | ❌ | `DepositBookingPolicy` não existe |
| U36 | Sessões prolongadas | 🔧 | `LongSessionBookingPolicy` existe; não injetada no fluxo |

### 1.5.1. Expediente do estabelecimento (BR1–BR5)

| ID | Regra | Estado | Evidência |
| --- | --- | --- | --- |
| BR1 | Expediente configurável pelo administrador | ✅ | `GET/PUT /api/v1/tenant/settings/schedule` |
| BR2 | Horário por dia da semana, dias podem estar fechados | ✅ | `EstablishmentSchedule`, `EstablishmentDaySchedule` |
| BR3 | Independe dos profissionais | ✅ | `AvailabilityService` não consulta o estabelecimento para agendar |
| BR4 | Agendamento depende só do profissional | ✅ | `AvailabilityService.GetAvailableSlotsAsync` |
| BR5 | Sem configuração, fechado 24/7 | ✅ | `EstablishmentSchedule.ClosedByDefault()` |

### 1.6. Site e presença digital

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U37 | Site individual por tenant | ✅ | `SiteController` (get, create, add section, preview, publish, rollback) |
| U38 | Subdomínio da plataforma | ✅ | `Site.Subdomain` + resolução por host em `PublicSiteReader` |
| U39 | Domínio próprio | ⚠️ | `Site.CustomDomain` com normalização e índice único; **sem endpoint de escrita** |
| U40 | Editor de conteúdo | ⚠️ | `POST /site/pages/{id}/sections`; sem edição de texto/imagem |
| U41 | Prévia multi-dispositivo | ❌ | Sem endpoint de prévia por dispositivo |
| U42 | Publicação com aprovação | ✅ | `OnboardingController` submit/approve/publish; publicar sem aprovar retorna `400` |
| U43 | Agendamento embutido no site | ✅ | `POST /api/v1/public/site/appointments` |
| U44 | Galeria de trabalhos | 🔧 | `SiteComponentType.Gallery` existe; sem fluxo de upload de portfólio |

### 1.7. Comunicação e notificações

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U45 | Confirmação de agendamento | 🔧 | `Notification` existe; nenhum fluxo cria |
| U46 | Lembretes | 🔧 | `NotificationDispatchJob` existe; fila nunca alimentada |
| U47 | Avisos de alteração | 🔧 | Idem |
| U48 | Canais de notificação | ⚠️ | `MessageChannel` tem WhatsApp/Email/Sms; só `IMessagingProvider` (WhatsApp) implementado |
| U48a | Controle de consumo do WhatsApp | 🚫 | **Descartado** — o Servia não intermedia a cobrança |
| U48b | Compra antecipada de créditos de WhatsApp | 🚫 | **Descartado** |
| U48c | Cobrança automática do consumo de WhatsApp | 🚫 | **Descartado** |
| U48d | Limites de consumo definidos pelo estabelecimento | 🚫 | **Descartado** para WhatsApp |
| U48e | Permissões por tipo de mensagem | ⚠️ | `MessagingPolicy` existe; `GET /whatsapp/policy`; sem endpoint de escrita |
| U48f | Aproveitamento do uso gratuito da Meta | 🚫 | **Descartado** |
| U49 | Templates de mensagem | ⚠️ | `MessageTemplate` + `GET/POST /whatsapp/templates`; sem estado de aprovação |
| U50 | Preferências de comunicação | ❌ | `CustomerPreference` não tem canal nem consentimento |

### 1.8. Pagamentos e financeiro

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U51 | Registro de pagamento | ✅ | `POST /api/v1/finance/payments` (presencial) |
| U52 | Pagamento online | 🔧 | `Payment.RegisterOnline` existe; sem fluxo |
| U53 | Formas de pagamento | ✅ | `PaymentMethod` (dinheiro, cartão, Pix, transferência) |
| U54 | Receitas e despesas | ✅ | `POST/GET /api/v1/finance/transactions` |
| U55 | Relatórios financeiros | ✅ | `GET /api/v1/finance/report` (agregação no banco) |
| U56 | Estornos e cancelamentos financeiros | ✅ | `POST /api/v1/finance/payments/{id}/refund` |
| U56a | Conciliação financeira | 🔧 | `Reconciliation` existe; sem fluxo |

### 1.9. Administração da plataforma

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| U57 | Painel administrativo da plataforma | ⚠️ | `POST /api/v1/platform/tenants`; sem interface |
| U57a | Separação de privilégios da plataforma | ✅ | `PlatformAdmin` isolado de permissões de tenant |
| U57b | Operação do WhatsApp como Solution Partner | 🚫 | **Descartado** — decisão de 2026-11-14 |
| U57c | Franquia mensal de IA por plano | ✅ | `Plan.IncludedAiUnits` + `AiUsageService` |
| U57d | Cobrança separada de WhatsApp e IA | 🚫 | **Descartado** para WhatsApp; IA mantém linha própria |
| U58 | Auditoria | 🔧 | `AuditEntry` imutável e testado; nenhuma ação grava |
| U59 | Observabilidade | ⚠️ | Health checks (`/health`, `/health/live`, `/health/ready`); sem logs estruturados, métricas ou trace |
| U60 | Backup e restauração | ⚠️ | `scripts/backup.sh` (pg_dump + checksum + retenção); sem agendamento nem registro em `TenantBackup` |

---

## 2. Módulos opcionais

### 2.1. Assinaturas, clubes e pacotes

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| M01 | Clube de assinatura | ✅ | `GET/POST /api/v1/modules/membership-plans`, `POST /modules/memberships` |
| M02 | Pacotes de serviços | ✅ | `GET/POST /api/v1/modules/packages` |
| M03 | Controle de benefícios | ⚠️ | `MembershipBenefit`, `PackageUsage`; consumo parcial |
| M04 | Cobrança recorrente | 🔧 | `MembershipRenewalJob` renova o período; sem cobrança |
| M05 | Gestão de inadimplência | ❌ | Sem fluxo |

### 2.2. Comissões e remuneração

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| M06 | Regras de comissão | ⚠️ | `CommissionRule` existe; sem endpoint de criação |
| M07 | Cálculo de comissões | ⚠️ | `GET /api/v1/finance/commissions/{professionalId}` |
| M08 | Extrato do profissional | ⚠️ | `GET /api/v1/finance/commissions/{professionalId}?from=&to=` retorna totais pendente/aprovado/pago/recebível; sem interface no painel |

### 2.3. Automação de atendimento (WhatsApp)

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| M09 | Atendimento automatizado | 🚫 | **Pausado** — integração com a Meta não iniciada |
| M10 | Agendamento por conversa | 🚫 | **Pausado** |
| M11 | Atendimento fora do horário | 🚫 | **Pausado** |
| M12 | Transferência para humano | ⚠️ | `POST /api/v1/whatsapp/conversations/{id}/escalate` (sem integração real) |
| M13 | Histórico de conversas | ⚠️ | `GET /api/v1/whatsapp/conversations` (sem integração real) |

### 2.4. Relacionamento e fidelização

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| M14 | Programa de fidelidade | ✅ | `GET/POST /api/v1/modules/loyalty/{customerId}` |
| M15 | Recuperação de clientes inativos | 🔧 | `ReactivationCampaign` existe; sem fluxo |
| M16 | Campanhas de comunicação | ❌ | Sem fluxo |
| M17 | Avaliações e NPS | ✅ | `POST /api/v1/modules/reviews`, `GET /api/v1/modules/nps` |

### 2.5. Geração de site por IA

| ID | Funcionalidade | Estado | Evidência |
| --- | --- | --- | --- |
| M18 | Geração inicial do site | ✅ | `OnboardingController` start/messages/generate; `OnboardingService` |
| M19 | Geração por referências | ⚠️ | `AiGenerationRequest` aceita até 5 URLs; sem análise de imagem |
| M20 | Personalização conversacional | ✅ | `POST /onboarding/sessions/{id}/adjustments` |
| M21 | Guardrails de conteúdo | ✅ | `AiChangeSet` valida componente e propriedade contra `SiteComponentSchema` |
| M22 | Registro de operações da IA | 🔧 | `AiOperationLog` existe; onboarding não registra |

---

## 3. Configurações e adaptações por segmento

| ID | Configuração | Estado | Evidência |
| --- | --- | --- | --- |
| S01 | Segmento do tenant | ✅ | `Tenant.Segment` (`SegmentType`) |
| S02 | Duração padrão e granularidade de horários | ⚠️ | Duração por serviço; granularidade fixa |
| S03 | Regras de sinal e no-show | ❌ | Sem política de sinal |
| S04 | Sessões prolongadas e múltiplos blocos | 🔧 | `LongSessionBookingPolicy` existe; não injetada |
| S05 | Histórico de procedimentos e fichas | ❌ | Sem fluxo |
| S06 | Pacotes de manutenção | ⚠️ | `ServicePackage`; sem regra de manutenção |
| S07 | Comissões e combos | ⚠️ | `CommissionRule`, `ServiceCombo`; fluxos parciais |
| S08 | Múltiplos profissionais por atendimento | ❌ | Um profissional por agendamento |
| S09 | Recursos por profissional (sala, maca, cadeira) | ❌ | Sem fluxo |
| S10 | Regras de utilização de benefícios | ⚠️ | `MembershipBenefit`; consumo parcial |
| S11 | Módulos habilitados por tenant | 🚫 | **Descartado** — todos os planos liberam todos os módulos |
| S12 | Identidade visual e tema do site | ✅ | `SiteTheme`, `Site.ChangeTheme` |
| S13 | Política de agendamento por segmento | 🔧 | `IBookingPolicy` existe; não injetada |
| S14 | Política de disponibilidade por segmento | 🔧 | `IAvailabilityPolicy` existe; não injetada |

---

## 4. Requisitos não funcionais

| ID | Requisito | Estado | Evidência |
| --- | --- | --- | --- |
| N01 | Isolamento de dados por `TenantId` | ✅ | Filtro global em `AppDbContext` |
| N01a | Isolamento em todas as camadas | ✅ | Claim `tenant_id`, filtro global, FK composta |
| N01b | Testes de isolamento cross-tenant | ✅ | 5 suítes (33 testes) |
| N01c | Concorrência no agendamento | ✅ | Transação `Serializable` + `ScheduleLock` |
| N02 | Segurança (hash, JWT, HTTPS) | ⚠️ | PBKDF2 600k, JWT 15 min, HTTPS/HSTS; **sem cookie `HttpOnly`, sem CSRF** |
| N03 | Autorização por papel e recurso | ✅ | `RequirePermissionAttribute` + claim `perm` |
| N04 | LGPD | ⚠️ | Anonimização ✅; consentimento ❌; retenção 🔧 |
| N05 | Auditoria | 🔧 | `AuditEntry` sem fluxo |
| N06 | Desempenho (p95 < 300 ms) | ⚠️ | Agregações no banco; sem medição de p95 |
| N07 | Disponibilidade 99,5% | ⚠️ | Health checks; sem monitoramento |
| N08 | Responsividade | ⚠️ | Frontend atual; será substituído |
| N09 | Acessibilidade WCAG 2.1 AA | ⚠️ | Parcial no frontend atual |
| N10 | Escalabilidade | ⚠️ | Multi-tenant; jobs sem lock distribuído |
| N11 | Observabilidade | ⚠️ | Health checks; sem logs estruturados, métricas ou trace |
| N12 | Testes | ✅ | 1011 atributos `[Fact]`/`[Theory]` |
| N13 | Testes de integração externa | ✅ | `ProviderTests`, `RetryAndIdempotencyTests` |
| N14 | Núcleo sem condicionais de segmento | ⚠️ | Políticas existem; não injetadas |

---

## 5. Lacunas e pendências

### 5.1. Resolvidas

| Item | Resolução |
| --- | --- |
| Build quebrado (`IOnboardingRepository`) | Resolvido — repositório criado e registrado |
| Onboarding com IA sem controller/persistência | Resolvido — `OnboardingController` (11 endpoints), DbSets, migration |
| Site público sem endpoint | Resolvido — `PublicSiteController`, `PublicSiteReader` |
| Upload de assets sem DbSet/DI/endpoint | Resolvido — `OnboardingAssetsController`, `SiteAssetRepository` |
| Billing sem escrita | Resolvido — `POST/PATCH/cancel` no `BillingController` |
| Webhooks ausentes | Resolvido — `WebhooksController` com HMAC e idempotência |
| Nenhum job em background | Resolvido — `JobHostedService` + 7 rotinas |
| Bootstrap do `PlatformAdmin` | Resolvido — comando `seed-admin` |
| Expediente derivado dos profissionais | Resolvido — `EstablishmentSchedule` independente (BR5) |
| `Administrator` sem permissões administrativas | Resolvido — todas as permissões de tenant, exceto `ManageBilling` |
| Contagem de profissionais por papel | Resolvido — `IsServiceProvider` independente do papel |

### 5.2. Pendentes

| # | Pendência | Origem |
| --- | --- | --- |
| P1 | Ciclo financeiro do billing (cobrança, fatura, inadimplência) | Auditoria ARCH |
| P2 | Política de consumo de IA: compra manual, aquisição automática, notificação | Decisão de produto |
| P3 | Cobrança de alteração pós-publicação | Tabela aprovada, não implementada |
| P4 | Cookie `HttpOnly` + CSRF + CORS | Decisão obrigatória |
| P5 | Observabilidade (logs, métricas, trace, alertas) | Auditoria ARCH |
| P6 | Notificações (criação, preferências, canais) | Auditoria ARCH |
| P7 | Recuperação de senha e convite de equipe | Catálogo |
| P8 | Endpoint de escrita de `CustomDomain` | Catálogo |
| P9 | Edição de serviço/profissional/cliente | Catálogo |
| P10 | Integração com WhatsApp Business Platform | Pausada, condicionada à Meta |

### 5.3. Descartadas

| Item | Motivo |
| --- | --- |
| Franquia, créditos e cobrança intermediada de WhatsApp | Decisão de 2026-11-14 |
| Cobrança ilimitada por token excedente de IA | Decisão de produto |
| Módulos habilitados por plano | Todos os planos liberam todos os módulos |
| Código acadêmico original | ADR-001 — sistema novo |

---

## 6. Separação dos contextos financeiros

| Contexto | Titular | Cobre |
| --- | --- | --- |
| **Financeiro do estabelecimento** | O tenant | Receitas, despesas, pagamentos, comissões, pacotes, clubes, relatórios |
| **Billing da plataforma** | O Servia | Planos, assinaturas, faturas, inadimplência, consumo de IA |

Nenhum dos dois referencia o outro em suas regras de negócio. Ver [`../arquitetura/ADR.md`](../arquitetura/ADR.md) ADR-020.

---

## 7. Fora de escopo da v1

- Aplicativo móvel nativo.
- Marketplace público de estabelecimentos.
- Integrações com ERPs e sistemas fiscais.
- Emissão de nota fiscal.
- Multi-idioma e multi-moeda.
- Programa de indicação entre tenants.