# Modelo de Classes — Domínio de Agendamento

> **Finalidade:** atender aos critérios **B01–B07** — modelo textual interpretável, com atributos, métodos, associações, multiplicidades, composição, contrato polimórfico e invariantes.
> **Fonte da verdade:** este documento especifica o modelo que será implementado no Servia (ADR-001). Deriva de `docs/classes.md`, que contém o modelo completo da plataforma; aqui está o recorte do domínio de agendamento.
> **Formato:** textual estruturado, legível por máquina e por humano.

---

## 1. Classes de domínio

### 1.1. `Appointment`

Agregado raiz do atendimento. Protege as transições de estado.

| Atributo | Tipo | Visibilidade | Observação |
| --- | --- | --- | --- |
| `Id` | `Guid` | público, `private set` | Identidade preservada em todas as transições |
| `TenantId` | `Guid` | público, `private set` | Isolamento multi-tenant |
| `CustomerId` | `Guid` | público, `private set` | Referência ao cliente |
| `ServiceId` | `Guid` | público, `private set` | Referência ao serviço |
| `ProfessionalId` | `Guid` | público, `private set` | Referência ao profissional |
| `Date` | `DateOnly` | público, `private set` | Data do atendimento |
| `Time` | `TimeOnly` | público, `private set` | Horário de início |
| `DurationMinutes` | `int` | público, `private set` | Duração do serviço no momento da reserva |
| `Status` | `AppointmentStatus` | público, `private set` | `Confirmed`, `Cancelled` ou `Completed` |
| `CancellationReason` | `string?` | público, `private set` | Motivo do cancelamento |
| `CreatedAt` | `DateTimeOffset` | público, `private set` | Instante de criação (UTC) |

| Método | Assinatura | Responsabilidade |
| --- | --- | --- |
| `IsConfirmed` | `bool` (propriedade calculada) | Indica se o estado é `Confirmed` |
| `Cancel` | `void Cancel(string? reason)` | Transição `Confirmed` → `Cancelled` |
| `Complete` | `void Complete(DateTimeOffset now)` | Transição `Confirmed` → `Completed`, rejeitando futuro |
| `Reschedule` | `void Reschedule(DateOnly date, TimeOnly time)` | Altera data e hora, mantendo `Confirmed` |
| `EnsureConfirmed` | `private void EnsureConfirmed(string message)` | Guarda de invariante de estado |

**Invariantes:**
- `CustomerId`, `ServiceId` e `ProfessionalId` não podem ser vazios.
- `Date` não pode ser `default`; `DurationMinutes` deve ser maior que zero.
- Só é possível transicionar a partir de `Confirmed`.

---

### 1.2. `WorkSchedule`

Jornada semanal de um profissional. Protege as regras de disponibilidade.

| Atributo | Tipo | Visibilidade |
| --- | --- | --- |
| `WorkDays` | `IReadOnlySet<DayOfWeek>` | público, `private set` |
| `StartTime` | `TimeOnly` | público, `private set` |
| `EndTime` | `TimeOnly` | público, `private set` |
| `SlotIntervalMinutes` | `int` | público, `private set` |

| Método | Assinatura | Responsabilidade |
| --- | --- | --- |
| `WorksOn` | `bool WorksOn(DateOnly date)` | Indica se trabalha na data |
| `FitsWithinSchedule` | `bool FitsWithinSchedule(TimeOnly time, int durationMinutes)` | Indica se o intervalo cabe na jornada |
| `GenerateSlots` | `IEnumerable<TimeOnly> GenerateSlots(DateOnly date, int durationMinutes)` | Gera os horários candidatos do dia |

**Invariantes:**
- Ao menos um dia de trabalho.
- `StartTime` estritamente anterior a `EndTime`.
- `SlotIntervalMinutes` entre 5 e 120.

---

### 1.3. `Professional`

Profissional que executa serviços.

| Atributo | Tipo | Visibilidade |
| --- | --- | --- |
| `Id` | `Guid` | público, `private set` |
| `TenantId` | `Guid` | público, `private set` |
| `UserId` | `Guid?` | público, `private set` — vínculo opcional com login |
| `Name` | `string` | público, `private set` |
| `Title` | `string?` | público, `private set` |
| `Bio` | `string?` | público, `private set` |
| `PhotoUrl` | `string?` | público, `private set` |
| `IsActive` | `bool` | público, `private set` |
| `Schedule` | `WorkSchedule` | público, `private set` — **composição** |

| Método | Assinatura | Responsabilidade |
| --- | --- | --- |
| `UpdateSchedule` | `void UpdateSchedule(WorkSchedule schedule)` | Substitui a jornada |
| `Deactivate` | `void Deactivate()` | Desativa o profissional |

---

### 1.4. `Service`

Serviço ofertado pelo estabelecimento.

| Atributo | Tipo | Visibilidade |
| --- | --- | --- |
| `Id` | `Guid` | público, `private set` |
| `TenantId` | `Guid` | público, `private set` |
| `CategoryId` | `Guid?` | público, `private set` |
| `Name` | `string` | público, `private set` |
| `Description` | `string?` | público, `private set` |
| `Price` | `Money` | público, `private set` |
| `DurationMinutes` | `int` | público, `private set` |
| `Highlight` | `bool` | público, `private set` |
| `IsActive` | `bool` | público, `private set` |

| Método | Assinatura | Responsabilidade |
| --- | --- | --- |
| `ChangePrice` | `void ChangePrice(Money price)` | Altera o preço com validação |
| `Deactivate` | `void Deactivate()` | Desativa preservando histórico |
| `Activate` | `void Activate()` | Reativa o serviço |

**Invariantes:** `DurationMinutes > 0`; `Price >= 0`; desativação preserva histórico.

---

### 1.5. `Customer`

Cliente do estabelecimento. Separado de `User` (ADR-009).

| Atributo | Tipo | Visibilidade |
| --- | --- | --- |
| `Id` | `Guid` | público, `private set` |
| `TenantId` | `Guid` | público, `private set` |
| `UserId` | `Guid?` | público, `private set` — vínculo opcional com login |
| `Name` | `string` | público, `private set` |
| `Phone` | `string` | público, `private set` |
| `Email` | `string?` | público, `private set` |
| `BirthDate` | `DateOnly?` | público, `private set` |
| `IsActive` | `bool` | público, `private set` |

| Método | Assinatura | Responsabilidade |
| --- | --- | --- |
| `UpdateContact` | `void UpdateContact(string phone, string? email)` | Atualiza contato com validação |
| `Anonymize` | `void Anonymize()` | Remove dados pessoais (LGPD) |
| `Deactivate` | `void Deactivate()` | Desativa o cadastro |

---

## 2. Associações e multiplicidades

| Origem | Destino | Multiplicidade | Implementação prevista |
| --- | --- | --- | --- |
| `Appointment` | `Customer` | `0..*` → `1` | `Appointment.CustomerId` + FK composta `(TenantId, CustomerId)` |
| `Appointment` | `Service` | `0..*` → `1` | `Appointment.ServiceId` + FK composta `(TenantId, ServiceId)` |
| `Appointment` | `Professional` | `0..*` → `1` | `Appointment.ProfessionalId` + FK composta `(TenantId, ProfessionalId)` |
| `Professional` | `Appointment` | `1` → `0..*` | Navegação inversa |
| `Customer` | `Appointment` | `1` → `0..*` | Navegação inversa |
| `Service` | `Appointment` | `1` → `0..*` | Navegação inversa |

**Verificação da multiplicidade:** um `Customer` pode ter zero ou muitos `Appointment`; cada `Appointment` pertence a exatamente um `Customer`. O mesmo vale para `Service` e `Professional`. As chaves estrangeiras são **compostas com `TenantId`** (ADR-016), impedindo associação entre tenants diferentes.

---

## 3. Composição

### `Professional` ◆—— `WorkSchedule`

**Relação:** composição (ciclo de vida dependente).

**Justificativa:** a jornada não existe sem o profissional. Não há sentido em uma jornada órfã, e ela não é compartilhada entre profissionais. Por isso é composição, e não associação.

**Implementação prevista:** `Professional.Schedule` é obrigatório e mapeado como *owned type* no EF Core — a jornada **não tem tabela própria**; suas colunas vivem na tabela `Professionals`.

---

## 4. Contrato polimórfico

O domínio possui um contrato de política de disponibilidade com duas implementações distintas, utilizadas efetivamente pela aplicação (ADR-018).

```text
«interface»
IAvailabilityPolicy
  + IsAvailable(WorkSchedule schedule, DateOnly date, TimeOnly time, int durationMinutes) : bool
  + Describe() : string
        ▲
        │ implementa
        ├──────────────────────────────┐
        │                              │
StandardAvailabilityPolicy      ExtendedAvailabilityPolicy
  (jornada padrão)                (tolerância no fim do
                                   expediente)
```

| Implementação | Comportamento |
| --- | --- |
| `StandardAvailabilityPolicy` | Exige que o intervalo caiba **estritamente** dentro da jornada |
| `ExtendedAvailabilityPolicy` | Aceita atendimento que termina até **5 minutos** após o fim do expediente |

**Uso efetivo:** `AppointmentService` recebe `IAvailabilityPolicy` por injeção de dependência e a utiliza na validação de horário e na geração de disponibilidade. A implementação é escolhida na composição da aplicação.

**Variação real:** as duas implementações produzem resultados diferentes para a mesma entrada — não é herança decorativa.

---

## 5. Invariantes e objetos responsáveis

| # | Invariante | Objeto responsável | Onde é protegida |
| --- | --- | --- | --- |
| I1 | Só agendamentos `Confirmed` mudam de estado | `Appointment` | `EnsureConfirmed` |
| I2 | Atendimento futuro não pode ser concluído | `Appointment` | `Complete` |
| I3 | Jornada exige início anterior ao fim | `WorkSchedule` | Construtor |
| I4 | Serviço deve caber na jornada | `WorkSchedule` | `FitsWithinSchedule` |
| I5 | Sem sobreposição de agendamentos ativos | `AppointmentService` | `HasOverlap` |

**Invariante que ultrapassa uma instância:** I5 não pode ser protegida por uma única entidade — depende do conjunto de agendamentos do profissional. Por isso o responsável é o serviço de aplicação, que consulta o repositório antes de criar. A garantia final contra concorrência é o índice único parcial em `ScheduleLock` (ADR-017).

---

## 6. Correspondência com o modelo completo

| Classe do modelo | Contexto em `docs/classes.md` |
| --- | --- |
| `Appointment` | Agendamento |
| `WorkSchedule` | Equipe |
| `Professional` | Equipe |
| `Service` | Catálogo |
| `Customer` | Clientes |
| `IAvailabilityPolicy` | Interface de domínio |

As decisões de modelagem e a comparação de alternativas estão em [`analise-do-modelo.md`](analise-do-modelo.md).
