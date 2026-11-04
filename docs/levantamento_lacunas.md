# Inventário do Código Legado — Base para a Migração (F1.4)

> **Status:** documento ativo, enxugado em 2026-11-04.
> **Propósito:** descrever o que **existe de fato** no repositório hoje. É o insumo da entrega **F1.4 do Roadmap** — "Migração do sistema atual como primeiro tenant".
> **Não é mais:** registro de lacunas nem de decisões pendentes. O diagnóstico original foi absorvido pelo catálogo `docs/funcionalidades.md`; as decisões estruturais estão em `docs/funcionalidades.md` §0, `docs/gerenciamento/ARCH.md` §13 e nos 14 ADRs.
> **Atenção:** este documento descreve o **estado atual do código**, não a arquitetura-alvo. Para o alvo, consulte `docs/gerenciamento/ARCH.md`.

---

## 1. O que existe hoje (código real)

### Backend — C# / .NET 10, 5 projetos em camadas

`Servia.Api → Servia.Application → Servia.Infrastructure → Servia.Domain` (+ `Servia.Tests`)

> Os projetos no código-fonte ainda usam o prefixo `BackAndre.*`, herdado da fase acadêmica. A renomeação física está planejada para uma etapa própria; a documentação já usa `Servia`.

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

### Documentação existente

- `README.md` raiz — identificação acadêmica no topo (preservada) e visão SaaS da plataforma.
- `backend/README.md`, `backend/ARQUITETURA.md`, `frontend/README.md`.
- `docs/descricao_produto.md` — visão SaaS multi-segmento.
- `docs/funcionalidades.md`, `docs/classes.md`, `docs/gerenciamento/` — documentação SDD da plataforma.

---

## 2. O que precisa mudar no código para a migração (F1.4)

Resumo operacional do que a entrega F1.4 exige sobre o código acima. O detalhamento de cada item está nas fases correspondentes do `Roadmap.md`.

| Item existente | Mudança necessária | Fase |
| --- | --- | --- |
| `User` com papéis `client`/`admin` | Separar `Customer` de `User`; papéis por tenant (`Owner`, `Administrator`, `Professional`, `Receptionist`, `Customer`) | F1.5, F2.1 |
| `Barber` + `BarberSchedule` | Generalizar para `Professional` (qualquer segmento) | F2.4 |
| Entidades sem `TenantId` | Adicionar `TenantId` e implementar `ITenantScoped`; filtro global no EF Core | F1.2, F1.3 |
| Repositórios concretos sem interfaces | Introduzir interfaces de repositório e `IUnitOfWork` | F1.3 |
| EF Core + SQLite | Migrar para PostgreSQL; SQLite apenas em testes locais | F0.7 |
| Sem Docker, sem CI | Dockerfile, `docker-compose` e pipeline no GitHub Actions | F0.1–F0.4 |
| Landing estática hardcoded | Site por tenant com subdomínio e publicação | F3 |
| Sem billing | `Tenant`, `Plan`, `Subscription`, `PlatformInvoice` e gateway Mercado Pago | F1.1, F4 |
| Sem auditoria | `AuditEntry` para ações sensíveis | F1.8 |
| Testes só de domínio | Testes de isolamento entre tenants obrigatórios no pipeline | F1.3, N01 |

---

## 3. Referências

| Documento | Papel |
| --- | --- |
| `docs/descricao_produto.md` | Visão de produto original |
| `docs/funcionalidades.md` | Catálogo de funcionalidades e decisões estruturais |
| `docs/classes.md` | Modelo de classes e diagrama Mermaid |
| `docs/gerenciamento/Roadmap.md` | Fases F0–F9 e entregas |
| `docs/gerenciamento/ADR.md` | 14 decisões arquiteturais aceitas |
| `docs/gerenciamento/ARCH.md` | Arquitetura-alvo da plataforma |
