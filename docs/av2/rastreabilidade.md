# Matriz de Rastreabilidade — Fluxos, Regras, Implementação e Testes

> **Finalidade:** atender ao critério **F05** — rastreabilidade entre os três fluxos, as cinco regras de negócio, suas implementações e seus testes.
> **Escopo:** domínio de agendamento do Servia (ADR-001). A coluna "Implementação" indica o artefato previsto; a coluna "Teste" indica o teste que comprova.
> **Regra de leitura:** nenhuma linha é considerada atendida sem teste correspondente.

---

## 1. Fluxos → Regras → Implementação → Testes

| Fluxo | Regra | Implementação | Teste de sucesso | Teste de violação |
| --- | --- | --- | --- | --- |
| F1 — Criar agendamento | R3 — jornada válida | `WorkSchedule` (construtor) | `WorkScheduleTests.Criar_Jornada_Valida_Aceita` | `WorkScheduleTests.Criar_Jornada_Inicio_Igual_Fim_Lanca_Excecao` |
| F1 — Criar agendamento | R4 — serviço cabe na jornada | `WorkSchedule.FitsWithinSchedule` | `WorkScheduleTests.FitsWithinSchedule_Aceita_Horario_Que_Termina_No_Fim` | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim` |
| F1 — Criar agendamento | R5 — sem sobreposição | `AppointmentService.HasOverlap` | `AppointmentServiceTests.Criar_Aceita_Horario_Livre` | `AppointmentServiceTests.Criar_Rejeita_Sobreposicao_De_Horario` |
| F2 — Cancelar agendamento | R1 — só confirmado transiciona | `Appointment.EnsureConfirmed` | `AppointmentTests.Cancelar_Agendamento_Confirmado_Muda_Para_Cancelado` | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Lanca_Excecao` |
| F3 — Concluir atendimento | R1 — só confirmado transiciona | `Appointment.EnsureConfirmed` | `AppointmentTests.Concluir_Atendimento_Passado_Muda_Para_Concluido` | `AppointmentTests.Concluir_Agendamento_Cancelado_Lanca_Excecao` |
| F3 — Concluir atendimento | R2 — não concluir futuro | `Appointment.Complete` | `AppointmentTests.Concluir_Atendimento_Passado_Muda_Para_Concluido` | `AppointmentTests.Concluir_Atendimento_Futuro_Lanca_Excecao` |

---

## 2. Contagem de cenários por regra

O critério **F01** exige testes de sucesso e de violação para cada uma das cinco regras, totalizando ao menos dez cenários.

| Regra | Cenários de sucesso | Cenários de violação | Total |
| --- | --- | --- | --- |
| R1 — só confirmado transiciona | 2 | 2 | 4 |
| R2 — não concluir futuro | 1 | 1 | 2 |
| R3 — jornada válida | 1 | 2 | 3 |
| R4 — serviço cabe na jornada | 1 | 2 | 3 |
| R5 — sem sobreposição | 2 | 2 | 4 |
| **Total** | **7** | **9** | **16** |

**Resultado:** 16 cenários, acima do mínimo de 10 exigido.

---

## 3. Invariantes → Testes

| Invariante | Objeto responsável | Teste |
| --- | --- | --- |
| I1 — só `Confirmed` muda de estado | `Appointment` | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| I2 — futuro não pode ser concluído | `Appointment` | `AppointmentTests.Concluir_Atendimento_Futuro_Lanca_Excecao` |
| I3 — jornada exige início anterior ao fim | `WorkSchedule` | `WorkScheduleTests.Criar_Jornada_Inicio_Maior_Fim_Lanca_Excecao` |
| I4 — serviço cabe na jornada | `WorkSchedule` | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim` |
| I5 — sem sobreposição | `AppointmentService` | `AppointmentServiceTests.Criar_Rejeita_Sobreposicao_De_Horario` |

---

## 4. Situações de falha → Testes

| Situação | Natureza | Teste |
| --- | --- | --- |
| S1 — violação de regra de negócio | Regra de negócio | `ExceptionHandlingTests.RegraDeNegocio_Retorna_400` |
| S2 — recurso inexistente | Excepcional esperada | `ExceptionHandlingTests.RecursoInexistente_Retorna_404` |
| S3 — falha inesperada | Exceção não prevista | `ExceptionHandlingTests.FalhaInesperada_Retorna_500_Sem_Vazar_Detalhe` |

---

## 5. Estados → Testes

| Estado | Transição | Teste |
| --- | --- | --- |
| `Confirmed` → `Cancelled` | Cancelar | `AppointmentTests.Cancelar_Agendamento_Confirmado_Muda_Para_Cancelado` |
| `Confirmed` → `Completed` | Concluir | `AppointmentTests.Concluir_Atendimento_Passado_Muda_Para_Concluido` |
| `Cancelled` (terminal) | Tentativa de cancelar | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| Identidade preservada | Todas as transições | `AppointmentTests.Transicoes_Preservam_Identidade` |

---

## 6. Polimorfismo → Testes

| Contrato | Implementação | Teste |
| --- | --- | --- |
| `IAvailabilityPolicy` | `StandardAvailabilityPolicy` | `AvailabilityPolicyTests.Standard_Aceita_Horario_Que_Cabe_Na_Jornada` |
| `IAvailabilityPolicy` | `ExtendedAvailabilityPolicy` | `AvailabilityPolicyTests.Extended_Aceita_Horario_Que_Ultrapassa_Fim_Dentro_Da_Tolerancia` |
| `IAvailabilityPolicy` | Divergência entre implementações | `AvailabilityPolicyTests.Implementacoes_Divergem_Para_A_Mesma_Entrada` |

---

## 7. Defeito → Teste de regressão

| Defeito | Cenário | Teste |
| --- | --- | --- |
| `DEF-001` — sobreposição cruzando meia-noite | 23:50 (30 min) vs 23:55 (30 min) | `AppointmentServiceTests.HasOverlap_Detecta_Sobreposicao_Que_Cruza_Meia_Noite` |
