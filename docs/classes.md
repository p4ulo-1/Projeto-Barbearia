# Modelo de Classes — Plataforma SaaS

> **Status:** baseline v1, derivado de `docs/funcionalidades.md`.
> **Escopo:** modelo de domínio completo da plataforma (multi-tenant), incluindo entidades já existentes e as novas.
> **Convenção:** entidades de negócio carregam `TenantId`; agregados protegem invariantes por métodos de domínio, não por setters públicos.

---

## 1. Visão por contexto delimitado (bounded context)

| Contexto | Responsabilidade | Agregados principais |
| --- | --- | --- |
| **Plataforma** | Tenants, planos, assinatura do SaaS, faturamento da plataforma | `Tenant`, `Plan`, `Subscription`, `PlatformInvoice` |
| **Identidade e Acesso** | Usuários, papéis, permissões, sessões | `User`, `Role`, `Permission`, `RefreshToken`, `Invitation` |
| **Catálogo** | Serviços, categorias, combos, vínculo com profissionais | `Service`, `ServiceCategory`, `ServiceVariant`, `ServiceCombo` |
| **Equipe** | Profissionais, jornada, bloqueios, vínculo com serviços | `Professional`, `WorkSchedule`, `ScheduleBlock`, `ProfessionalService` |
| **Agendamento** | Disponibilidade, reservas, estados do atendimento | `Appointment`, `AvailabilitySlot`, `WaitlistEntry` |
| **Clientes** | Cadastro, histórico, preferências, consentimento | `Customer`, `CustomerPreference`, `ConsentRecord` |
| **Financeiro** | Pagamentos, receitas, despesas, comissões | `Payment`, `Transaction`, `CommissionRule`, `CommissionEntry` |
| **Assinaturas do negócio** | Clubes, pacotes, benefícios | `MembershipPlan`, `Membership`, `ServicePackage`, `PackageUsage` |
| **Site e Conteúdo** | Site por tenant, páginas, seções, publicação | `Site`, `SitePage`, `SiteSection`, `SitePublication` |
| **IA** | Geração e personalização de site, guardrails | `AiGenerationRequest`, `AiChangeSet`, `AiOperationLog` |
| **Comunicação** | Notificações, templates, conversas WhatsApp | `Notification`, `MessageTemplate`, `Conversation`, `Message` |
| **Auditoria** | Registro imutável de ações sensíveis | `AuditEntry` |

---

## 2. Classes por contexto

### 2.1. Contexto Plataforma

#### `Tenant`
Agregado raiz do estabelecimento. Todo dado de negócio pertence a um tenant.

| Membro | Tipo | Descrição |
| --- | --- | --- |
| `Id` | `Guid` | Identificador |
| `Slug` | `string` | Identificador único usado no subdomínio |
| `Name` | `string` | Nome do estabelecimento |
| `Segment` | `SegmentType` | Segmento de atuação |
| `Document` | `string` | CNPJ ou CPF do responsável |
| `Status` | `TenantStatus` | `Trial`, `Active`, `Suspended`, `Cancelled` |
| `CreatedAt` | `DateTimeOffset` | Data de criação |
| `Subscription` | `Subscription` | Assinatura vigente |
| `EnabledModules` | `IReadOnlyCollection<ModuleType>` | Módulos habilitados |

Métodos: `Activate()`, `Suspend(reason)`, `Cancel()`, `EnableModule(module)`, `DisableModule(module)`, `IsModuleEnabled(module)`.

Invariantes: `Slug` imutável após publicação do site; não é possível desabilitar módulo com contrato ativo em uso.

#### `Plan`
Plano comercial da plataforma.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `Code` | `string` |
| `Name` | `string` |
| `MonthlyPrice` | `Money` |
| `IncludedProfessionals` | `int` |
| `ExtraProfessionalPrice` | `Money` |
| `IncludedModules` | `IReadOnlyCollection<ModuleType>` |
| `IsActive` | `bool` |

#### `Subscription`
Assinatura de um tenant a um plano.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `PlanId` | `Guid` |
| `Status` | `SubscriptionStatus` |
| `CurrentPeriodStart` / `CurrentPeriodEnd` | `DateOnly` |
| `TrialEndsAt` | `DateOnly?` |
| `GatewayCustomerId` | `string?` |
| `GatewaySubscriptionId` | `string?` |

Métodos: `StartTrial(days)`, `Renew(period)`, `MarkPastDue()`, `Cancel(atPeriodEnd)`.

#### `PlatformInvoice`
Fatura da plataforma contra o tenant.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `SubscriptionId` | `Guid` |
| `Amount` | `Money` |
| `DueDate` | `DateOnly` |
| `Status` | `InvoiceStatus` |
| `PaidAt` | `DateTimeOffset?` |
| `GatewayPaymentId` | `string?` |

Métodos: `MarkPaid(gatewayPaymentId)`, `MarkFailed(reason)`, `IsOverdue(now)`.

#### `ModuleType` (enum)
`Membership`, `Packages`, `Commissions`, `WhatsAppAutomation`, `Loyalty`, `AiSiteBuilder`, `OnlinePayments`.

#### `SegmentType` (enum)
`Barbershop`, `HairSalon`, `NailStudio`, `Aesthetics`, `BrowsAndLashes`, `Tattoo`, `Massage`, `Makeup`, `Waxing`, `Braids`, `Other`.

#### `TenantStatus` / `SubscriptionStatus` / `InvoiceStatus` (enums)
Estados de ciclo de vida do tenant, da assinatura e da fatura.

---

### 2.2. Contexto Identidade e Acesso

#### `User`
Usuário da plataforma. Pode ser membro da equipe de um tenant ou cliente final.

| Membro | Tipo | Descrição |
| --- | --- | --- |
| `Id` | `Guid` | |
| `TenantId` | `Guid?` | Nulo para administradores da plataforma |
| `Name` | `string` | |
| `Email` | `string` | Único por tenant |
| `Phone` | `string?` | |
| `PasswordHash` | `string` | |
| `Role` | `Role` | Papel dentro do tenant |
| `IsActive` | `bool` | |
| `LastLoginAt` | `DateTimeOffset?` | |

Métodos: `ChangePassword(newHash)`, `Deactivate()`, `Activate()`, `UpdateProfile(...)`.

#### `Role` (enum)
`PlatformAdmin`, `Owner`, `Administrator`, `Professional`, `Receptionist`, `Customer`.

#### `Permission`
Permissão granular associada a um papel.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `Code` | `string` |
| `Description` | `string` |

#### `RefreshToken`
Token de renovação de sessão.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `UserId` | `Guid` |
| `TokenHash` | `string` |
| `ExpiresAt` | `DateTimeOffset` |
| `RevokedAt` | `DateTimeOffset?` |

Métodos: `Revoke()`, `IsValid(now)`.

#### `Invitation`
Convite de membro da equipe.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Email` | `string` |
| `Role` | `Role` |
| `TokenHash` | `string` |
| `ExpiresAt` | `DateTimeOffset` |
| `AcceptedAt` | `DateTimeOffset?` |

Métodos: `Accept(userId)`, `Expire()`, `IsValid(now)`.

---

### 2.3. Contexto Catálogo

#### `Service`
Serviço oferecido pelo tenant. Evolui da entidade atual.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `CategoryId` | `Guid?` |
| `Name` | `string` |
| `Description` | `string?` |
| `Price` | `Money` |
| `DurationMinutes` | `int` |
| `Highlight` | `bool` |
| `IsActive` | `bool` |

Métodos: `Update(...)`, `Deactivate()`, `Activate()`, `ChangePrice(money)`.

Invariantes: `DurationMinutes > 0`; `Price >= 0`; desativação preserva histórico.

#### `ServiceCategory`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Name` | `string` |
| `SortOrder` | `int` |

#### `ServiceVariant`
Variação de preço/duração por profissional ou por tipo.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `ServiceId` | `Guid` |
| `ProfessionalId` | `Guid?` |
| `Label` | `string` |
| `Price` | `Money` |
| `DurationMinutes` | `int` |

#### `ServiceCombo`
Combinação de serviços executados na mesma sessão.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Name` | `string` |
| `ComboPrice` | `Money` |
| `Items` | `IReadOnlyCollection<ComboItem>` |

Métodos: `TotalDuration()`, `AddItem(serviceId, quantity)`.

#### `ComboItem`
| Membro | Tipo |
| --- | --- |
| `ServiceId` | `Guid` |
| `Quantity` | `int` |

---

### 2.4. Contexto Equipe

#### `Professional`
Profissional que executa serviços. Substitui `Barber`, generalizando para qualquer segmento.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `UserId` | `Guid?` | Vínculo com usuário de login |
| `Name` | `string` |
| `Title` | `string?` | Cargo exibido |
| `Bio` | `string?` |
| `PhotoUrl` | `string?` |
| `IsActive` | `bool` |
| `Schedule` | `WorkSchedule` |

Métodos: `AssignService(serviceId)`, `RemoveService(serviceId)`, `UpdateSchedule(schedule)`, `Deactivate()`.

#### `WorkSchedule`
Jornada semanal. Evolui de `BarberSchedule`.

| Membro | Tipo |
| --- | --- |
| `WorkDays` | `IReadOnlySet<DayOfWeek>` |
| `StartTime` | `TimeOnly` |
| `EndTime` | `TimeOnly` |
| `SlotIntervalMinutes` | `int` |

Métodos: `WorksOn(date)`, `FitsWithinSchedule(time, duration)`, `GenerateSlots(date, duration)`.

Invariantes: `StartTime < EndTime`; `SlotIntervalMinutes` entre 5 e 120.

#### `ScheduleBlock`
Bloqueio pontual de agenda.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `ProfessionalId` | `Guid` |
| `Start` | `DateTimeOffset` |
| `End` | `DateTimeOffset` |
| `Reason` | `string?` |

Métodos: `Overlaps(start, end)`.

#### `ProfessionalService`
Vínculo N:N entre profissional e serviço.

| Membro | Tipo |
| --- | --- |
| `ProfessionalId` | `Guid` |
| `ServiceId` | `Guid` |

---

### 2.5. Contexto Agendamento

#### `Appointment`
Agregado raiz do atendimento. Mantém as regras de transição de estado já existentes.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `CustomerId` | `Guid` |
| `ProfessionalId` | `Guid` |
| `ServiceId` | `Guid` |
| `Date` | `DateOnly` |
| `Time` | `TimeOnly` |
| `DurationMinutes` | `int` |
| `Status` | `AppointmentStatus` |
| `Notes` | `string?` |
| `RequiresDeposit` | `bool` |
| `DepositPaid` | `bool` |
| `CancellationReason` | `string?` |

Métodos: `Confirm()`, `Cancel(reason)`, `Complete(now)`, `Reschedule(date, time)`, `MarkNoShow()`, `RegisterDeposit(paymentId)`.

Invariantes: só transições a partir de `Confirmed`; `Complete` rejeita data futura; conflito de horário bloqueado no agregado de agenda.

#### `AppointmentStatus` (enum)
`Pending`, `Confirmed`, `Cancelled`, `Completed`, `NoShow`.

#### `AvailabilitySlot`
Objeto de valor calculado, não persistido.

| Membro | Tipo |
| --- | --- |
| `Date` | `DateOnly` |
| `Time` | `TimeOnly` |
| `ProfessionalId` | `Guid` |
| `IsAvailable` | `bool` |

#### `WaitlistEntry`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `CustomerId` | `Guid` |
| `ServiceId` | `Guid` |
| `PreferredDate` | `DateOnly` |
| `Status` | `WaitlistStatus` |

Métodos: `Offer(slot)`, `Accept()`, `Expire()`.

---

### 2.6. Contexto Clientes

#### `Customer`
Cliente final do tenant. Separado de `User` porque nem todo cliente tem login.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `UserId` | `Guid?` |
| `Name` | `string` |
| `Phone` | `string` |
| `Email` | `string?` |
| `BirthDate` | `DateOnly?` |
| `Notes` | `string?` |
| `IsActive` | `bool` |
| `CreatedAt` | `DateTimeOffset` |

Métodos: `Anonymize()`, `Deactivate()`, `UpdateContact(...)`.

#### `CustomerPreference`
| Membro | Tipo |
| --- | --- |
| `CustomerId` | `Guid` |
| `PreferredProfessionalId` | `Guid?` |
| `Notes` | `string?` |

#### `ConsentRecord`
Registro de consentimento LGPD.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `CustomerId` | `Guid` |
| `Purpose` | `ConsentPurpose` |
| `Granted` | `bool` |
| `GrantedAt` | `DateTimeOffset` |
| `RevokedAt` | `DateTimeOffset?` |
| `Source` | `string` |

Métodos: `Revoke(now)`.

---

### 2.7. Contexto Financeiro

#### `Payment`
Pagamento de um atendimento ou de uma assinatura do negócio.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `AppointmentId` | `Guid?` |
| `MembershipId` | `Guid?` |
| `Amount` | `Money` |
| `Method` | `PaymentMethod` |
| `Status` | `PaymentStatus` |
| `Gateway` | `GatewayType?` |
| `GatewayPaymentId` | `string?` |
| `PaidAt` | `DateTimeOffset?` |

Métodos: `Confirm(gatewayPaymentId)`, `Fail(reason)`, `Refund(amount, reason)`.

#### `PaymentMethod` (enum)
`Cash`, `CreditCard`, `DebitCard`, `Pix`, `BankTransfer`, `Other`.

#### `PaymentStatus` (enum)
`Pending`, `Paid`, `Failed`, `Refunded`, `PartiallyRefunded`.

#### `Transaction`
Lançamento financeiro do tenant.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Type` | `TransactionType` |
| `CategoryId` | `Guid?` |
| `Amount` | `Money` |
| `OccurredAt` | `DateTimeOffset` |
| `Description` | `string` |
| `PaymentId` | `Guid?` |

#### `CommissionRule`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `ProfessionalId` | `Guid?` |
| `ServiceId` | `Guid?` |
| `Percentage` | `decimal` |
| `FixedAmount` | `Money?` |

Métodos: `CalculateFor(amount)`.

#### `CommissionEntry`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `ProfessionalId` | `Guid` |
| `AppointmentId` | `Guid` |
| `Amount` | `Money` |
| `ReferencePeriod` | `DateOnly` |
| `Status` | `CommissionStatus` |

---

### 2.8. Contexto Assinaturas do Negócio

#### `MembershipPlan`
Clube de assinatura oferecido pelo tenant.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Name` | `string` |
| `MonthlyPrice` | `Money` |
| `Benefits` | `IReadOnlyCollection<MembershipBenefit>` |
| `IsActive` | `bool` |

#### `MembershipBenefit`
| Membro | Tipo |
| --- | --- |
| `ServiceId` | `Guid?` |
| `QuantityPerPeriod` | `int` |
| `DiscountPercentage` | `decimal?` |

#### `Membership`
Assinatura de um cliente ao clube.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `CustomerId` | `Guid` |
| `MembershipPlanId` | `Guid` |
| `Status` | `MembershipStatus` |
| `CurrentPeriodStart` / `CurrentPeriodEnd` | `DateOnly` |

Métodos: `Renew()`, `Suspend()`, `Cancel()`, `ConsumeBenefit(serviceId)`.

#### `ServicePackage`
Pacote pré-pago de sessões.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Name` | `string` |
| `Price` | `Money` |
| `SessionsIncluded` | `int` |
| `ValidityDays` | `int` |

#### `PackageUsage`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `PackageId` | `Guid` |
| `CustomerId` | `Guid` |
| `SessionsRemaining` | `int` |
| `ExpiresAt` | `DateOnly` |

Métodos: `Consume()`, `IsExpired(now)`.

---

### 2.9. Contexto Site e Conteúdo

#### `Site`
Site público de um tenant.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Subdomain` | `string` |
| `CustomDomain` | `string?` |
| `Theme` | `SiteTheme` |
| `Status` | `SiteStatus` |
| `PublishedVersionId` | `Guid?` |

Métodos: `Publish(versionId)`, `SetCustomDomain(domain)`, `ChangeTheme(theme)`.

#### `SitePage`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `SiteId` | `Guid` |
| `Slug` | `string` |
| `Title` | `string` |
| `SortOrder` | `int` |
| `Sections` | `IReadOnlyCollection<SiteSection>` |

#### `SiteSection`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `PageId` | `Guid` |
| `ComponentType` | `string` |
| `SortOrder` | `int` |
| `Configuration` | `json` |

Métodos: `Reconfigure(configuration)`, `MoveTo(order)`.

#### `SiteTheme`
| Membro | Tipo |
| --- | --- |
| `PrimaryColor` | `string` |
| `SecondaryColor` | `string` |
| `FontFamily` | `string` |
| `LayoutStyle` | `string` |

#### `SitePublication`
Versão publicada do site.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `SiteId` | `Guid` |
| `Version` | `int` |
| `Snapshot` | `json` |
| `PublishedAt` | `DateTimeOffset` |
| `PublishedBy` | `Guid` |

Métodos: `Rollback()`.

---

### 2.10. Contexto IA

#### `AiGenerationRequest`
Solicitação de geração ou alteração de site.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `SiteId` | `Guid` |
| `Kind` | `AiRequestKind` |
| `Prompt` | `string` |
| `References` | `IReadOnlyCollection<string>` |
| `Status` | `AiRequestStatus` |
| `CreatedAt` | `DateTimeOffset` |

#### `AiChangeSet`
Conjunto de alterações propostas pela IA.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `RequestId` | `Guid` |
| `ProposedSnapshot` | `json` |
| `Status` | `ChangeSetStatus` |
| `ApprovedBy` | `Guid?` |
| `ApprovedAt` | `DateTimeOffset?` |

Métodos: `Approve(userId)`, `Reject(reason)`, `IsApproved()`.

Invariantes: nenhuma alteração chega ao site publicado sem `Approve`.

#### `AiOperationLog`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `RequestId` | `Guid` |
| `Model` | `string` |
| `TokensIn` / `TokensOut` | `int` |
| `Cost` | `decimal` |
| `GuardrailViolations` | `json?` |

---

### 2.11. Contexto Comunicação

#### `Notification`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `CustomerId` | `Guid?` |
| `Channel` | `NotificationChannel` |
| `TemplateId` | `Guid?` |
| `Payload` | `json` |
| `Status` | `NotificationStatus` |
| `ScheduledAt` | `DateTimeOffset?` |
| `SentAt` | `DateTimeOffset?` |

Métodos: `Schedule(at)`, `MarkSent()`, `MarkFailed(reason)`.

#### `MessageTemplate`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `Key` | `string` |
| `Channel` | `NotificationChannel` |
| `Body` | `string` |
| `IsActive` | `bool` |

#### `Conversation`
Conversa de WhatsApp com um cliente.

| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid` |
| `CustomerId` | `Guid?` |
| `Phone` | `string` |
| `Status` | `ConversationStatus` |
| `AssignedTo` | `Guid?` |

Métodos: `Assign(userId)`, `Close()`, `EscalateToHuman()`.

#### `Message`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `ConversationId` | `Guid` |
| `Direction` | `MessageDirection` |
| `Body` | `string` |
| `SentAt` | `DateTimeOffset` |
| `ExternalId` | `string?` |

---

### 2.12. Contexto Auditoria

#### `AuditEntry`
| Membro | Tipo |
| --- | --- |
| `Id` | `Guid` |
| `TenantId` | `Guid?` |
| `ActorId` | `Guid?` |
| `Action` | `string` |
| `EntityType` | `string` |
| `EntityId` | `Guid` |
| `Changes` | `json` |
| `OccurredAt` | `DateTimeOffset` |
| `IpAddress` | `string?` |

Somente inserção; sem métodos de alteração ou exclusão.

---

## 3. Objetos de valor compartilhados

| Tipo | Membros | Regras |
| --- | --- | --- |
| `Money` | `Amount: decimal`, `Currency: string` | `Amount >= 0`; moeda ISO 4217 |
| `DateRange` | `Start: DateOnly`, `End: DateOnly` | `Start <= End` |
| `TimeRange` | `Start: TimeOnly`, `End: TimeOnly` | `Start < End` |
| `PhoneNumber` | `CountryCode`, `Number` | Normalização E.164 |
| `EmailAddress` | `Value` | Validação de formato |

---

## 4. Interfaces de domínio e aplicação

| Interface | Responsabilidade |
| --- | --- |
| `ITenantScoped` | Marca entidades de negócio que possuem `TenantId`; base do filtro global do EF Core |
| `ITenantContext` | Resolve o tenant da requisição e alimenta o filtro global |
| `IPaymentGateway` | Abstrai o gateway de pagamento; implementação confirmada: Mercado Pago (cobrança, assinatura, estorno, webhook) |
| `IMessagingProvider` | Abstrai Cloud API da Meta e provedores alternativos |
| `IAiSiteGenerator` | Abstrai o provedor de LLM para geração de site |
| `INotificationSender` | Envio por e-mail, WhatsApp e SMS |
| `IRepository<T>` | Contrato genérico de repositório por agregado |
| `IUnitOfWork` | Transação e commit por caso de uso |
| `IClock` | Abstração de tempo para testes determinísticos |

---

## 5. Diagrama de classes (Mermaid)

```mermaid
classDiagram
    direction LR

    %% ===== PLATAFORMA =====
    class Tenant {
        +Guid Id
        +string Slug
        +string Name
        +SegmentType Segment
        +string Document
        +TenantStatus Status
        +DateTimeOffset CreatedAt
        +Activate()
        +Suspend(reason)
        +Cancel()
        +EnableModule(module)
        +DisableModule(module)
        +IsModuleEnabled(module) bool
    }

    class Plan {
        +Guid Id
        +string Code
        +string Name
        +Money MonthlyPrice
        +int IncludedProfessionals
        +Money ExtraProfessionalPrice
        +bool IsActive
    }

    class Subscription {
        +Guid Id
        +Guid TenantId
        +Guid PlanId
        +SubscriptionStatus Status
        +DateOnly CurrentPeriodStart
        +DateOnly CurrentPeriodEnd
        +DateOnly? TrialEndsAt
        +string? GatewaySubscriptionId
        +StartTrial(days)
        +Renew(period)
        +MarkPastDue()
        +Cancel(atPeriodEnd)
    }

    class PlatformInvoice {
        +Guid Id
        +Guid SubscriptionId
        +Money Amount
        +DateOnly DueDate
        +InvoiceStatus Status
        +DateTimeOffset? PaidAt
        +MarkPaid(gatewayPaymentId)
        +MarkFailed(reason)
        +IsOverdue(now) bool
    }

    %% ===== IDENTIDADE =====
    class User {
        +Guid Id
        +Guid? TenantId
        +string Name
        +string Email
        +string? Phone
        +string PasswordHash
        +Role Role
        +bool IsActive
        +DateTimeOffset? LastLoginAt
        +ChangePassword(newHash)
        +Deactivate()
        +Activate()
        +UpdateProfile()
    }

    class RefreshToken {
        +Guid Id
        +Guid UserId
        +string TokenHash
        +DateTimeOffset ExpiresAt
        +DateTimeOffset? RevokedAt
        +Revoke()
        +IsValid(now) bool
    }

    class Invitation {
        +Guid Id
        +Guid TenantId
        +string Email
        +Role Role
        +string TokenHash
        +DateTimeOffset ExpiresAt
        +DateTimeOffset? AcceptedAt
        +Accept(userId)
        +Expire()
        +IsValid(now) bool
    }

    %% ===== CATÁLOGO =====
    class Service {
        +Guid Id
        +Guid TenantId
        +Guid? CategoryId
        +string Name
        +string? Description
        +Money Price
        +int DurationMinutes
        +bool Highlight
        +bool IsActive
        +Update()
        +Deactivate()
        +Activate()
        +ChangePrice(money)
    }

    class ServiceCategory {
        +Guid Id
        +Guid TenantId
        +string Name
        +int SortOrder
    }

    class ServiceVariant {
        +Guid Id
        +Guid ServiceId
        +Guid? ProfessionalId
        +string Label
        +Money Price
        +int DurationMinutes
    }

    class ServiceCombo {
        +Guid Id
        +Guid TenantId
        +string Name
        +Money ComboPrice
        +TotalDuration() int
        +AddItem(serviceId, quantity)
    }

    class ComboItem {
        +Guid ServiceId
        +int Quantity
    }

    %% ===== EQUIPE =====
    class Professional {
        +Guid Id
        +Guid TenantId
        +Guid? UserId
        +string Name
        +string? Title
        +string? Bio
        +string? PhotoUrl
        +bool IsActive
        +AssignService(serviceId)
        +RemoveService(serviceId)
        +UpdateSchedule(schedule)
        +Deactivate()
    }

    class WorkSchedule {
        +IReadOnlySet~DayOfWeek~ WorkDays
        +TimeOnly StartTime
        +TimeOnly EndTime
        +int SlotIntervalMinutes
        +WorksOn(date) bool
        +FitsWithinSchedule(time, duration) bool
        +GenerateSlots(date, duration)
    }

    class ScheduleBlock {
        +Guid Id
        +Guid ProfessionalId
        +DateTimeOffset Start
        +DateTimeOffset End
        +string? Reason
        +Overlaps(start, end) bool
    }

    class ProfessionalService {
        +Guid ProfessionalId
        +Guid ServiceId
    }

    %% ===== AGENDAMENTO =====
    class Appointment {
        +Guid Id
        +Guid TenantId
        +Guid CustomerId
        +Guid ProfessionalId
        +Guid ServiceId
        +DateOnly Date
        +TimeOnly Time
        +int DurationMinutes
        +AppointmentStatus Status
        +bool RequiresDeposit
        +bool DepositPaid
        +string? CancellationReason
        +Confirm()
        +Cancel(reason)
        +Complete(now)
        +Reschedule(date, time)
        +MarkNoShow()
        +RegisterDeposit(paymentId)
    }

    class AvailabilitySlot {
        +DateOnly Date
        +TimeOnly Time
        +Guid ProfessionalId
        +bool IsAvailable
    }

    class WaitlistEntry {
        +Guid Id
        +Guid TenantId
        +Guid CustomerId
        +Guid ServiceId
        +DateOnly PreferredDate
        +WaitlistStatus Status
        +Offer(slot)
        +Accept()
        +Expire()
    }

    %% ===== CLIENTES =====
    class Customer {
        +Guid Id
        +Guid TenantId
        +Guid? UserId
        +string Name
        +string Phone
        +string? Email
        +DateOnly? BirthDate
        +bool IsActive
        +DateTimeOffset CreatedAt
        +Anonymize()
        +Deactivate()
        +UpdateContact()
    }

    class CustomerPreference {
        +Guid CustomerId
        +Guid? PreferredProfessionalId
        +string? Notes
    }

    class ConsentRecord {
        +Guid Id
        +Guid CustomerId
        +ConsentPurpose Purpose
        +bool Granted
        +DateTimeOffset GrantedAt
        +DateTimeOffset? RevokedAt
        +Revoke(now)
    }

    %% ===== FINANCEIRO =====
    class Payment {
        +Guid Id
        +Guid TenantId
        +Guid? AppointmentId
        +Guid? MembershipId
        +Money Amount
        +PaymentMethod Method
        +PaymentStatus Status
        +GatewayType? Gateway
        +string? GatewayPaymentId
        +Confirm(gatewayPaymentId)
        +Fail(reason)
        +Refund(amount, reason)
    }

    class Transaction {
        +Guid Id
        +Guid TenantId
        +TransactionType Type
        +Money Amount
        +DateTimeOffset OccurredAt
        +string Description
    }

    class CommissionRule {
        +Guid Id
        +Guid TenantId
        +Guid? ProfessionalId
        +Guid? ServiceId
        +decimal Percentage
        +Money? FixedAmount
        +CalculateFor(amount) Money
    }

    class CommissionEntry {
        +Guid Id
        +Guid TenantId
        +Guid ProfessionalId
        +Guid AppointmentId
        +Money Amount
        +DateOnly ReferencePeriod
        +CommissionStatus Status
    }

    %% ===== ASSINATURAS DO NEGÓCIO =====
    class MembershipPlan {
        +Guid Id
        +Guid TenantId
        +string Name
        +Money MonthlyPrice
        +bool IsActive
    }

    class MembershipBenefit {
        +Guid? ServiceId
        +int QuantityPerPeriod
        +decimal? DiscountPercentage
    }

    class Membership {
        +Guid Id
        +Guid TenantId
        +Guid CustomerId
        +Guid MembershipPlanId
        +MembershipStatus Status
        +DateOnly CurrentPeriodStart
        +DateOnly CurrentPeriodEnd
        +Renew()
        +Suspend()
        +Cancel()
        +ConsumeBenefit(serviceId)
    }

    class ServicePackage {
        +Guid Id
        +Guid TenantId
        +string Name
        +Money Price
        +int SessionsIncluded
        +int ValidityDays
    }

    class PackageUsage {
        +Guid Id
        +Guid TenantId
        +Guid PackageId
        +Guid CustomerId
        +int SessionsRemaining
        +DateOnly ExpiresAt
        +Consume()
        +IsExpired(now) bool
    }

    %% ===== SITE =====
    class Site {
        +Guid Id
        +Guid TenantId
        +string Subdomain
        +string? CustomDomain
        +SiteStatus Status
        +Guid? PublishedVersionId
        +Publish(versionId)
        +SetCustomDomain(domain)
        +ChangeTheme(theme)
    }

    class SitePage {
        +Guid Id
        +Guid SiteId
        +string Slug
        +string Title
        +int SortOrder
    }

    class SiteSection {
        +Guid Id
        +Guid PageId
        +string ComponentType
        +int SortOrder
        +json Configuration
        +Reconfigure(configuration)
        +MoveTo(order)
    }

    class SiteTheme {
        +string PrimaryColor
        +string SecondaryColor
        +string FontFamily
        +string LayoutStyle
    }

    class SitePublication {
        +Guid Id
        +Guid SiteId
        +int Version
        +json Snapshot
        +DateTimeOffset PublishedAt
        +Guid PublishedBy
        +Rollback()
    }

    %% ===== IA =====
    class AiGenerationRequest {
        +Guid Id
        +Guid TenantId
        +Guid SiteId
        +AiRequestKind Kind
        +string Prompt
        +AiRequestStatus Status
        +DateTimeOffset CreatedAt
    }

    class AiChangeSet {
        +Guid Id
        +Guid RequestId
        +json ProposedSnapshot
        +ChangeSetStatus Status
        +Guid? ApprovedBy
        +DateTimeOffset? ApprovedAt
        +Approve(userId)
        +Reject(reason)
        +IsApproved() bool
    }

    class AiOperationLog {
        +Guid Id
        +Guid RequestId
        +string Model
        +int TokensIn
        +int TokensOut
        +decimal Cost
    }

    %% ===== COMUNICAÇÃO =====
    class Notification {
        +Guid Id
        +Guid TenantId
        +Guid? CustomerId
        +NotificationChannel Channel
        +NotificationStatus Status
        +DateTimeOffset? ScheduledAt
        +DateTimeOffset? SentAt
        +Schedule(at)
        +MarkSent()
        +MarkFailed(reason)
    }

    class MessageTemplate {
        +Guid Id
        +Guid TenantId
        +string Key
        +NotificationChannel Channel
        +string Body
        +bool IsActive
    }

    class Conversation {
        +Guid Id
        +Guid TenantId
        +Guid? CustomerId
        +string Phone
        +ConversationStatus Status
        +Guid? AssignedTo
        +Assign(userId)
        +Close()
        +EscalateToHuman()
    }

    class Message {
        +Guid Id
        +Guid ConversationId
        +MessageDirection Direction
        +string Body
        +DateTimeOffset SentAt
        +string? ExternalId
    }

    %% ===== AUDITORIA =====
    class AuditEntry {
        +Guid Id
        +Guid? TenantId
        +Guid? ActorId
        +string Action
        +string EntityType
        +Guid EntityId
        +json Changes
        +DateTimeOffset OccurredAt
    }

    %% ===== RELACIONAMENTOS =====
    Tenant "1" --> "1" Subscription : possui
    Plan "1" --> "0..*" Subscription : define
    Subscription "1" --> "0..*" PlatformInvoice : fatura
    Tenant "1" --> "0..*" User : membros
    User "1" --> "0..*" RefreshToken : sessões
    Tenant "1" --> "0..*" Invitation : convites

    Tenant "1" --> "0..*" Service : catálogo
    ServiceCategory "1" --> "0..*" Service : agrupa
    Service "1" --> "0..*" ServiceVariant : variações
    ServiceCombo "1" *-- "1..*" ComboItem : compõe

    Tenant "1" --> "0..*" Professional : equipe
    Professional "1" *-- "1" WorkSchedule : jornada
    Professional "1" --> "0..*" ScheduleBlock : bloqueios
    Professional "1" --> "0..*" ProfessionalService : executa
    Service "1" --> "0..*" ProfessionalService : atribuído

    Tenant "1" --> "0..*" Appointment : agenda
    Customer "1" --> "0..*" Appointment : realiza
    Professional "1" --> "0..*" Appointment : atende
    Service "1" --> "0..*" Appointment : referencia
    Appointment ..> AvailabilitySlot : calcula
    Tenant "1" --> "0..*" WaitlistEntry : fila

    Tenant "1" --> "0..*" Customer : clientes
    User "0..1" --> "0..*" Customer : login
    Customer "1" *-- "0..1" CustomerPreference : preferências
    Customer "1" --> "0..*" ConsentRecord : consentimentos

    Tenant "1" --> "0..*" Payment : pagamentos
    Appointment "0..1" --> "0..*" Payment : cobra
    Tenant "1" --> "0..*" Transaction : lançamentos
    Tenant "1" --> "0..*" CommissionRule : regras
    Professional "1" --> "0..*" CommissionEntry : comissões
    Appointment "1" --> "0..1" CommissionEntry : gera

    Tenant "1" --> "0..*" MembershipPlan : clubes
    MembershipPlan "1" *-- "1..*" MembershipBenefit : benefícios
    MembershipPlan "1" --> "0..*" Membership : assinaturas
    Customer "1" --> "0..*" Membership : assina
    Tenant "1" --> "0..*" ServicePackage : pacotes
    ServicePackage "1" --> "0..*" PackageUsage : usos
    Customer "1" --> "0..*" PackageUsage : consome
    Membership "0..1" --> "0..*" Payment : cobra

    Tenant "1" --> "0..1" Site : site
    Site "1" *-- "0..*" SitePage : páginas
    SitePage "1" *-- "0..*" SiteSection : seções
    Site "1" *-- "1" SiteTheme : tema
    Site "1" --> "0..*" SitePublication : versões

    Tenant "1" --> "0..*" AiGenerationRequest : solicita
    Site "1" --> "0..*" AiGenerationRequest : alvo
    AiGenerationRequest "1" --> "0..*" AiChangeSet : propõe
    AiGenerationRequest "1" --> "0..*" AiOperationLog : registra
    AiChangeSet ..> SitePublication : aprova

    Tenant "1" --> "0..*" Notification : notificações
    Tenant "1" --> "0..*" MessageTemplate : templates
    Tenant "1" --> "0..*" Conversation : conversas
    Customer "0..1" --> "0..*" Conversation : participa
    Conversation "1" *-- "0..*" Message : mensagens
    Appointment "0..1" --> "0..*" Notification : dispara

    Tenant "0..1" --> "0..*" AuditEntry : auditoria
```

---

## 6. Notas de modelagem

1. **`Customer` separado de `User`** — nem todo cliente final terá login. `User` representa identidade de acesso; `Customer` representa o relacionamento comercial com o tenant. O vínculo é opcional.
2. **`Professional` substitui `Barber`** — o modelo atual é específico de barbearia. A generalização permite atender todos os segmentos sem duplicar entidades.
3. **`TenantId` em toda entidade de negócio** — o filtro global do EF Core garante isolamento; testes automatizados devem falhar se o filtro for removido.
   - **Exceção:** `Tenant` é a raiz do agregado e não implementa `ITenantScoped` (seria autorreferência). Ela é acessada apenas pelo contexto administrativo da plataforma.
   - **Exceção:** `Plan` é entidade global da plataforma, compartilhada por todos os tenants, e também não implementa `ITenantScoped`.
4. **`AiChangeSet` como barreira de aprovação** — nenhuma alteração gerada por IA chega ao site publicado sem `Approve`, atendendo ao requisito de revisão explícita.
5. **`AuditEntry` somente inserção** — sem métodos de alteração, garantindo imutabilidade do log.
6. **`Money` como objeto de valor** — evita ambiguidade de moeda e centraliza a regra de valor não negativo.
7. **`AvailabilitySlot` não é persistido** — é calculado a partir de `WorkSchedule`, `ScheduleBlock` e `Appointment` existentes.
