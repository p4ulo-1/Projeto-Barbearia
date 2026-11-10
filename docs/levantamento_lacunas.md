# Inventário do Código Acadêmico — Registro Histórico

> **Status:** registro histórico. Não é insumo de implementação.
> **Propósito:** documentar o que existe no código acadêmico em `backend/` e `frontend/`, para preservar a memória da origem do projeto.
> **Decisão vigente:** o Servia é um **sistema novo** (ADR-001). O código acadêmico **não** é reaproveitado, não é base de implementação e não é fonte de regras.
> **Atenção:** este documento descreve o **estado do código acadêmico**, não a arquitetura-alvo. Para o alvo, consulte `docs/gerenciamento/ARCH.md`.

---

## 1. O que existe no código acadêmico

### Backend — C# / .NET 10, 5 projetos em camadas

`BackAndre.Api → BackAndre.Application → BackAndre.Infrastructure → BackAndre.Domain` (+ `BackAndre.Tests`)

| Camada | Conteúdo |
| --- | --- |
| Domain | `User` (papéis `client`/`admin`), `Service`, `Barber` (+ `BarberSchedule`), `Appointment` (confirmado/cancelado/concluído), `DomainRuleException` |
| Application | `AuthService`, `UserService`, `ServiceService`, `BarberService`, `AppointmentService` (slots de 30 min, lead time de 30 min, detecção de conflito) + DTOs |
| Infrastructure | `AppDbContext`, `DbInitializer` (seed), repositórios concretos (sem interfaces), 1 migration, `JwtTokenService` |
| Api | Controllers de auth/users/services/barbers/appointments, middleware de exceções, Swagger, CORS |
| Tests | xUnit cobrindo apenas regras de domínio (agendamentos, jornada de trabalho) |

Persistência: **EF Core + SQLite** (`barbearia.db`), sem Docker, sem CI.

### Frontend — React 19 + TypeScript + TanStack Start/Router + Tailwind 4 + shadcn

- Rotas: `index` (landing), `agendamento` (wizard de 4 passos), `auth`, `conta`, `admin` (Agenda/Serviços/Clientes).
- Estado: React Context (`StoreProvider`), sem Redux/Zustand.
- API client tipado com JWT em `localStorage` e evento de 401.

---

## 2. Por que não é reaproveitado

O código acadêmico foi construído para um único estabelecimento, com um modelo fixo de barbearia e sem multi-tenancy. Adaptá-lo exigiria:

- refatorar todas as entidades para incluir `TenantId` e isolamento em todas as camadas;
- substituir a persistência e reescrever as migrations;
- reescrever o frontend por superfícies de produto;
- remover decisões do modelo single-shop que contaminariam o desenho novo.

O custo dessa adaptação supera o custo de construir o domínio do zero a partir de `docs/classes.md`, que já define o modelo correto para multi-tenancy e multi-segmento.

**Decisão registrada em:** ADR-001.

---

## 3. O que o código acadêmico demonstra

O código acadêmico serve como **evidência de domínio mínimo** — mostra quais áreas o Servia precisa cobrir. Não serve como referência de implementação.

| Área | O que demonstra |
| --- | --- |
| Autenticação e usuários | Login, cadastro, perfil, troca de senha |
| Catálogo de serviços | Cadastro, preço, duração, ativação |
| Profissionais e jornada | Profissional com jornada semanal |
| Agendamento | Disponibilidade, criação, remarcação, cancelamento, conclusão |
| Site e agendamento público | Landing e wizard de agendamento |

---

## 4. Referências

| Documento | Papel |
| --- | --- |
| `docs/descricao_produto.md` | Visão de produto original |
| `docs/diretrizes.md` | Diretrizes e conclusões que revisaram o projeto |
| `docs/funcionalidades.md` | Catálogo de funcionalidades e decisões estruturais |
| `docs/classes.md` | Modelo de classes e diagrama Mermaid |
| `docs/gerenciamento/Roadmap.md` | Fases F0–F9 e entregas |
| `docs/gerenciamento/ADR.md` | 21 decisões arquiteturais aceitas |
| `docs/gerenciamento/ARCH.md` | Arquitetura-alvo da plataforma |
