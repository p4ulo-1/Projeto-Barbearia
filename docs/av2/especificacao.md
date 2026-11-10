# Especificação Verificável — Fluxos, Regras de Negócio e Critérios de Aceitação

> **Finalidade:** atender ao critério **G01** — especificação verificável dos três fluxos e das cinco regras de negócio, com condições e critérios de aceitação.
> **Escopo:** o domínio de agendamento do Servia, que será construído do zero (ADR-001). Esta especificação é a **fonte de verdade** para a implementação — não descreve código existente.
> **Princípio:** cada critério abaixo é verificável por um teste automatizado nomeado. Nenhum critério depende de julgamento subjetivo.

---

## 1. Contexto do domínio

O Servia gerencia agendamentos de serviços entre clientes e profissionais. Cada profissional possui uma jornada de trabalho semanal. Um agendamento ocupa um intervalo de tempo de um profissional e passa por estados ao longo do seu ciclo de vida.

**Entidades envolvidas:**

| Entidade | Papel no domínio |
| --- | --- |
| `Appointment` | Agregado raiz do atendimento; protege as transições de estado |
| `WorkSchedule` | Jornada semanal; protege a regra de disponibilidade |
| `Professional` | Profissional que executa o serviço |
| `Service` | Serviço ofertado, com preço e duração |
| `Customer` | Cliente do estabelecimento |

> **Nota de nomenclatura:** o modelo usa `Professional` e `WorkSchedule` (ADR-010), generalizando para qualquer segmento. `Customer` é separado de `User` (ADR-009).

---

## 2. Fluxos de negócio

### F1 — Criar agendamento

**Ator:** cliente.
**Objetivo:** reservar um horário com um profissional para um serviço.

**Condições de entrada:**
- Cliente, serviço e profissional identificados.
- Data e horário informados.

**Fluxo principal:**
1. O sistema verifica se o profissional trabalha na data solicitada.
2. O sistema verifica se o horário comporta a duração do serviço dentro da jornada, segundo a política de disponibilidade do tenant.
3. O sistema verifica se não há sobreposição com outro agendamento ativo do mesmo profissional.
4. O agendamento é criado no estado `Confirmed`.

**Fluxos de exceção:**
- Profissional não trabalha na data → agendamento rejeitado.
- Horário fora da jornada → agendamento rejeitado.
- Sobreposição com agendamento ativo → agendamento rejeitado.

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| F1-CA1 | Agendamento válido é criado com estado `Confirmed` | `AppointmentTests.Criar_Agendamento_Valido_Inicia_Confirmado` |
| F1-CA2 | Agendamento em dia não trabalhado é rejeitado | `WorkScheduleTests.WorksOn_Retorna_Falso_Em_Dia_Nao_Trabalhado` |
| F1-CA3 | Horário que ultrapassa o fim da jornada é rejeitado | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim` |
| F1-CA4 | Sobreposição com agendamento ativo é rejeitada | `AppointmentServiceTests.Criar_Rejeita_Sobreposicao_De_Horario` |

---

### F2 — Cancelar agendamento

**Ator:** cliente ou equipe.
**Objetivo:** desistir de um atendimento confirmado.

**Condições de entrada:**
- Agendamento existente no estado `Confirmed`.

**Fluxo principal:**
1. O sistema verifica que o agendamento está `Confirmed`.
2. O estado passa para `Cancelled`.

**Fluxos de exceção:**
- Agendamento já cancelado → operação rejeitada.
- Agendamento já concluído → operação rejeitada.

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| F2-CA1 | Cancelamento de agendamento confirmado muda o estado para `Cancelled` | `AppointmentTests.Cancelar_Agendamento_Confirmado_Muda_Para_Cancelado` |
| F2-CA2 | Cancelamento de agendamento já cancelado é rejeitado | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Lanca_Excecao` |
| F2-CA3 | Após a rejeição, o estado permanece `Cancelled` (sem inconsistência) | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |

---

### F3 — Concluir atendimento

**Ator:** equipe do estabelecimento.
**Objetivo:** registrar que o atendimento foi realizado.

**Condições de entrada:**
- Agendamento existente no estado `Confirmed`.
- Data e hora do atendimento já ocorreram.

**Fluxo principal:**
1. O sistema verifica que o agendamento está `Confirmed`.
2. O sistema verifica que o horário do atendimento não é futuro.
3. O estado passa para `Completed`.

**Fluxos de exceção:**
- Atendimento futuro → operação rejeitada.
- Agendamento cancelado → operação rejeitada.

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| F3-CA1 | Conclusão de atendimento passado muda o estado para `Completed` | `AppointmentTests.Concluir_Atendimento_Passado_Muda_Para_Concluido` |
| F3-CA2 | Conclusão de atendimento futuro é rejeitada | `AppointmentTests.Concluir_Atendimento_Futuro_Lanca_Excecao` |
| F3-CA3 | Conclusão de agendamento cancelado é rejeitada | `AppointmentTests.Concluir_Agendamento_Cancelado_Lanca_Excecao` |

---

## 3. Regras de negócio

### R1 — Só agendamentos confirmados podem mudar de estado

**Enunciado:** as transições `Cancel`, `Complete` e `Reschedule` só são permitidas a partir do estado `Confirmed`.

**Objeto responsável:** `Appointment` (método privado `EnsureConfirmed`).

**Invariante protegida:** um agendamento em estado terminal (`Cancelled` ou `Completed`) nunca volta a ser alterado.

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| R1-CA1 | Cancelar a partir de `Confirmed` é permitido | `AppointmentTests.Cancelar_Agendamento_Confirmado_Muda_Para_Cancelado` |
| R1-CA2 | Cancelar a partir de `Cancelled` é rejeitado | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Lanca_Excecao` |
| R1-CA3 | Remarcar a partir de `Cancelled` é rejeitado | `AppointmentTests.Remarcar_Agendamento_Cancelado_Lanca_Excecao` |

---

### R2 — Não é possível concluir um atendimento futuro

**Enunciado:** a conclusão exige que a data e hora do agendamento sejam anteriores ou iguais ao instante atual.

**Objeto responsável:** `Appointment.Complete`.

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| R2-CA1 | Concluir atendimento passado é permitido | `AppointmentTests.Concluir_Atendimento_Passado_Muda_Para_Concluido` |
| R2-CA2 | Concluir atendimento futuro é rejeitado | `AppointmentTests.Concluir_Atendimento_Futuro_Lanca_Excecao` |

---

### R3 — A jornada exige início anterior ao fim

**Enunciado:** uma jornada de trabalho só é válida se o horário de início for estritamente anterior ao de fim.

**Objeto responsável:** `WorkSchedule` (construtor).

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| R3-CA1 | Jornada com início anterior ao fim é aceita | `WorkScheduleTests.Criar_Jornada_Valida_Aceita` |
| R3-CA2 | Jornada com início igual ao fim é rejeitada | `WorkScheduleTests.Criar_Jornada_Inicio_Igual_Fim_Lanca_Excecao` |
| R3-CA3 | Jornada com início posterior ao fim é rejeitada | `WorkScheduleTests.Criar_Jornada_Inicio_Maior_Fim_Lanca_Excecao` |

---

### R4 — O serviço deve caber integralmente na jornada

**Enunciado:** um horário só é válido se o intervalo `[início, início + duração]` estiver contido em `[início da jornada, fim da jornada]`.

**Objeto responsável:** `WorkSchedule.FitsWithinSchedule`.

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| R4-CA1 | Horário que termina exatamente no fim da jornada é aceito | `WorkScheduleTests.FitsWithinSchedule_Aceita_Horario_Que_Termina_No_Fim` |
| R4-CA2 | Horário que ultrapassa o fim da jornada é rejeitado | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim` |
| R4-CA3 | Horário antes da abertura é rejeitado | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Antes_Da_Abertura` |

---

### R5 — Não pode haver sobreposição de agendamentos ativos

**Enunciado:** dois agendamentos do mesmo profissional não podem ocupar intervalos sobrepostos, considerando apenas agendamentos não cancelados.

**Objeto responsável:** `AppointmentService` (método `HasOverlap`).

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| R5-CA1 | Agendamento sobreposto é rejeitado | `AppointmentServiceTests.Criar_Rejeita_Sobreposicao_De_Horario` |
| R5-CA2 | Agendamento em horário livre é aceito | `AppointmentServiceTests.Criar_Aceita_Horario_Livre` |
| R5-CA3 | Agendamento cancelado não bloqueia o horário | `AppointmentServiceTests.Criar_Ignora_Agendamento_Cancelado` |
| R5-CA4 | Sobreposição que cruza a meia-noite é detectada | `AppointmentServiceTests.HasOverlap_Detecta_Sobreposicao_Que_Cruza_Meia_Noite` |

---

## 4. Estados do agendamento

`Appointment` possui três estados significativos e preserva sua identidade (`Id`) em todas as transições.

| Estado | Significado | Transições permitidas |
| --- | --- | --- |
| `Confirmed` | Agendamento ativo | → `Cancelled`, → `Completed`, remarcar (permanece `Confirmed`) |
| `Cancelled` | Agendamento desfeito | nenhuma (terminal) |
| `Completed` | Atendimento realizado | nenhuma (terminal) |

```text
                 ┌──────────────┐
                 │  Confirmed   │◄──── Reschedule (mantém estado)
                 └──────┬───────┘
                        │
          ┌─────────────┴─────────────┐
          ▼                           ▼
   ┌──────────────┐           ┌──────────────┐
   │  Cancelled   │           │  Completed   │
   └──────────────┘           └──────────────┘
      (terminal)                 (terminal)
```

**Critério de aceitação da invariante de estado:**

| # | Critério | Verificação |
| --- | --- | --- |
| E-CA1 | Transição proibida é rejeitada sem alterar o estado persistido | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| E-CA2 | O identificador é preservado em todas as transições | `AppointmentTests.Transicoes_Preservam_Identidade` |

---

## 5. Situações de falha

O sistema distingue três naturezas de falha, conforme o Cap. 13 do livro de referência.

| # | Situação | Natureza | Comportamento esperado |
| --- | --- | --- | --- |
| S1 | Violação de regra de negócio no domínio | Regra de negócio | Lança `DomainRuleException`; API responde **400** com mensagem de negócio |
| S2 | Recurso inexistente solicitado pela API | Situação excepcional esperada | API responde **404** com mensagem genérica |
| S3 | Falha inesperada de infraestrutura | Exceção não prevista | API responde **500** com `TraceId`, **sem** expor stack trace ou detalhe interno |

**Critérios de aceitação:**

| # | Critério | Verificação |
| --- | --- | --- |
| S1-CA | Violação de regra retorna 400 com a mensagem do domínio | `ExceptionHandlingTests.RegraDeNegocio_Retorna_400` |
| S2-CA | Recurso inexistente retorna 404 | `ExceptionHandlingTests.RecursoInexistente_Retorna_404` |
| S3-CA | Falha inesperada retorna 500 sem vazar detalhe interno | `ExceptionHandlingTests.FalhaInesperada_Retorna_500_Sem_Vazar_Detalhe` |

---

## 6. Rastreabilidade resumida

| Fluxo | Regras envolvidas | Estados |
| --- | --- | --- |
| F1 — Criar agendamento | R3, R4, R5 | cria em `Confirmed` |
| F2 — Cancelar agendamento | R1 | `Confirmed` → `Cancelled` |
| F3 — Concluir atendimento | R1, R2 | `Confirmed` → `Completed` |

A matriz completa está em [`rastreabilidade.md`](rastreabilidade.md).
