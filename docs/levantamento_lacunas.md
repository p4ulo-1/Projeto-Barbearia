# Código Acadêmico — Registro Histórico

> **Status:** registro histórico. O código descrito aqui **não existe mais** no repositório.
> **Data da remoção:** 2026-11-10.
> **Decisão vigente:** o Servia é um sistema novo (ADR-001). O código acadêmico nunca foi base de implementação.
> **Como recuperar:** `git log --diff-filter=D -- backend frontend` identifica o commit da remoção; o código está no commit imediatamente anterior.

---

## 1. Por que este documento existe

O projeto nasceu como aplicação acadêmica de uma barbearia. Quando a visão cresceu para uma plataforma SaaS multi-segmento, o código original deixou de servir: era single-shop, sem multi-tenancy, com modelo fixo de barbearia.

O `ADR-001` decidiu construir o Servia do zero. Este documento preserva a memória do que existia, para que a origem não se perca e para que ninguém tente "recuperar" o código sem saber por que ele foi descartado.

---

## 2. O que existia

### Backend — C# / .NET 10, 5 projetos em camadas

| Projeto | Papel |
| --- | --- |
| `BackAndre.Api` | Entrada HTTP, Swagger, CORS, middleware de exceções |
| `BackAndre.Application` | Casos de uso: auth JWT, usuários, serviços, barbeiros, agendamentos |
| `BackAndre.Domain` | Entidades: `User`, `Service`, `Barber`, `BarberSchedule`, `Appointment` |
| `BackAndre.Infrastructure` | EF Core + SQLite, uma migration, seed de demonstração |
| `BackAndre.Tests` | xUnit, testes de domínio |

**Regras implementadas:** geração de slots de 30 minutos, detecção de conflito de horário, estados de agendamento (confirmado, cancelado, concluído).

### Frontend — React 19 + TypeScript + TanStack

| Superfície | Conteúdo |
| --- | --- |
| Landing | Página estática hardcoded |
| Agendamento | Wizard de 4 passos |
| Auth | Login e cadastro |
| Conta | Área do cliente |
| Admin | Agenda, Serviços, Clientes |

Estado via React Context; API client tipado com JWT em `localStorage`.

---

## 3. Por que foi descartado

| Limitação | Impacto |
| --- | --- |
| **Single-shop** | Nenhuma entidade `Tenant`. Todo o modelo assume um único estabelecimento |
| **Modelo fixo de barbearia** | `Barber` em vez de `Professional`; sem suporte a salão, nail, clínica, tatuagem |
| **Sem multi-tenancy** | Refatorar todas as entidades, a persistência e o frontend seria mais caro do que construir novo |
| **Frontend por página** | O SaaS exige site por tenant, gerado e publicado — não páginas fixas |
| **Sem billing, WhatsApp, IA** | Nada disso existia, e cada um exige modelo próprio |
| **SQLite** | Não atende produção multi-tenant |

O `ADR-001` registra a análise completa das alternativas.

---

## 4. O que o Servia construiu no lugar

O código novo está em `src/`, com 10 fases concluídas (F0–F9):

| Contexto | Entidades principais |
| --- | --- |
| Tenancy | `Tenant`, `Plan`, `Subscription`, `ITenantScoped`, `ITenantContext` |
| Identity | `User`, `Role`, `Permission`, `RefreshToken`, `Invitation` |
| Appointments | `Appointment`, `WorkSchedule`, `WaitlistEntry`, `ScheduleLock` |
| Catalog | `Service`, `ServiceCategory`, `ServiceVariant`, `ServiceCombo` |
| Team | `Professional`, `ProfessionalService`, `ScheduleBlock` |
| Customers | `Customer`, `CustomerPreference` |
| Sites | `Site`, `SitePage`, `SiteSection`, `SiteTheme`, `SitePublication` |
| Billing | `PlatformInvoice`, `UsageQuota`, `CreditPurchase`, `IPaymentGateway` |
| Finance | `Payment`, `Transaction`, `CommissionRule`, `CommissionEntry` |
| Messaging | `Conversation`, `Message`, `MessageTemplate`, `Notification` |
| Memberships | `MembershipPlan`, `Membership`, `ServicePackage`, `PackageUsage` |
| Loyalty | `LoyaltyAccount`, `ReactivationCampaign`, `ServiceReview` |
| Ai | `AiChangeSet`, `AiGenerationRequest`, `AiOperationLog` |
| Operations | `OperationMetrics`, `AlertRule`, `TenantBackup`, `DataSubjectRequest` |

**Verificação:** 664 testes automatizados, build limpo, teste de carga aprovado em ambiente Docker com PostgreSQL.

---

## 5. O que permanece do período acadêmico

| Item | Onde está |
| --- | --- |
| Identificação acadêmica (UNIFESO, disciplina, professor, integrantes) | `README.md` |
| Decisão de construir sistema novo | `ADR-001` |
| Este registro histórico | Este documento |
| Código original | Histórico do Git, commit anterior à remoção |

---

## 6. Referências

- `docs/gerenciamento/ADR.md` — ADR-001, decisão de construir sistema novo e remover o código acadêmico
- `docs/gerenciamento/ARCH.md` — arquitetura-alvo do Servia
- `docs/gerenciamento/Roadmap.md` — fases de construção
- `docs/funcionalidades.md` — catálogo de funcionalidades
