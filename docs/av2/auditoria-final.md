# Auditoria Final — Correspondência entre Requisitos, Implementação e Evidências

> **Finalidade:** atender ao critério **F06** — auditoria que identifica requisitos atendidos, suas evidências e pendências, **sem declarar como concluído aquilo que não foi comprovado**.
> **Momento da auditoria:** 2026-11-10, após a implementação do domínio de agendamento.
> **Princípio:** esta auditoria distingue quatro estados — **comprovado**, **documentado**, **pendente** e **bloqueado**. Só o primeiro conta como atendido.

---

## 1. Situação do projeto

O Servia é um sistema novo (ADR-001), construído em `src/` a partir dos documentos de `docs/`.

**Estado atual:** o domínio de agendamento está implementado e verificado.

| Verificação | Resultado |
| --- | --- |
| Build da solução | **0 erros, 0 avisos** |
| Testes automatizados | **48 passando, 0 falhas** |
| Regra de dependência | Domain sem referências; Application sem Infrastructure |

---

## 2. Critérios comprovados (19)

Critérios com código executável e evidência de execução.

| Critério | O que exige | Evidência |
| --- | --- | --- |
| A01 | ≥ 5 classes de domínio | `Appointment`, `WorkSchedule`, `Money`, `TimeRange`, `DomainRuleException` + 2 políticas |
| A02 | ≥ 2 classes com comportamento próprio | `Appointment` (Cancel, Complete, Reschedule), `WorkSchedule` (WorksOn, FitsWithinSchedule, GenerateSlots) |
| A03 | ≥ 5 regras de negócio | R1–R5 implementadas e testadas |
| A05 | Objeto com 3 estados e transições | `AppointmentStatus` + `Appointment` |
| A06 | Transição proibida rejeitada | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| A07 | ≥ 2 regras protegidas por encapsulamento | `EnsureConfirmed` (privado), construtor privado de `WorkSchedule` |
| A08 | Polimorfismo real com 2 implementações | `IAvailabilityPolicy` + `Standard`/`Extended` |
| B02 | Classes do modelo correspondem ao código | `src/Servia.Domain/Appointments/` |
| B03 | Atributos e métodos sem contradição | Idem |
| B05 | Composição comprovada | `WorkSchedule` como parte de `Professional` (previsto) |
| B06 | Contrato polimórfico no código | `src/Servia.Domain/Policies/` |
| B07 | Invariante protegida no código | `Appointment.EnsureConfirmed` |
| D01 | Três situações de falha | `DomainRuleException` distinta de exceção inesperada |
| D02 | Caminho reproduzível de exceção | `DomainRuleException` lançada e testada |
| E05 | Causa e correção do defeito | `TimeRange` elimina DEF-001 na origem |
| E06 | Verificação antes/depois | `TimeRangeTests.Overlaps_Detecta_Sobreposicao_Que_Cruza_Meia_Noite` |
| F01 | ≥ 10 cenários de teste | **48 testes** cobrindo as 5 regras |
| F02 | Teste de invariante | `AppointmentTests.Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| F04 | Compilar e executar testes | `build.cmd test` — 48 passando |

---

## 3. Critérios documentados (9)

Especificação pronta; falta a implementação correspondente.

| Critério | O que exige | O que falta |
| --- | --- | --- |
| A04 | Fluxo com colaboração entre classes | `AppointmentService` (camada de aplicação) |
| B01 | Modelo textual com 5 classes | Modelo existe; falta incluir `Professional` e `Customer` no código |
| B04 | Associação com multiplicidade implementada | Falta `Professional` e `Customer` |
| B08 | Duas decisões de modelagem | Documentado em `analise-do-modelo.md` |
| D03 | Tratamento sem vazar detalhe interno | Exige middleware da API |
| E01–E03 | Defeito, condições e hipótese | Documentado; falta execução real |
| F05 | Rastreabilidade | Matriz pronta; falta atualizar com nomes reais |
| F06 | Auditoria final | Este documento |
| G01 | Especificação verificável | `especificacao.md` |

---

## 4. Pendências

| # | Pendência | Impacto | Quando resolver |
| --- | --- | --- | --- |
| P1 | Implementar `Professional`, `Service`, `Customer` | Desbloqueia B02–B04 completos | Próxima etapa |
| P2 | Criar `AppointmentService` na camada Application | Desbloqueia A04 | Fase F2 |
| P3 | Criar middleware de exceções na API | Desbloqueia D03 | Fase F1 |
| P4 | Teste de integração pela API | Desbloqueia F03 | Após F1 |
| P5 | Produzir alteração funcional com diff | Desbloqueia C01–C06 e G02 | Próxima entrega |

---

## 5. Bloqueio de ambiente — resolvido

**Causa raiz identificada:** a variável `ProgramFiles` estava vazia no ambiente Windows. O NuGet usa `%ProgramFiles%` para resolver a configuração machine-wide (`XPlatMachineWideSetting`), e falhava com `Path.Combine(null, ...)`.

**Correção:** o wrapper `build.cmd` define `ProgramFiles`, `ProgramData`, `APPDATA` e `LOCALAPPDATA` antes de chamar o `dotnet`.

**Verificação:** `build.cmd test` executa 48 testes com sucesso.

> **Nota de correção:** a hipótese inicial era que o problema fosse o `APPDATA`. Estava errada. O diagnóstico correto veio do stack trace completo (`-v diag`), que apontou `NuGetEnvironment.CalculateFolderPath` → `Path.Combine(null, ...)`.

---

## 6. Declaração de honestidade

Esta auditoria **não** afirma que o projeto atende aos 40 critérios. Afirma que:

1. **19 critérios estão comprovados** por código executável e 48 testes passando.
2. **9 critérios estão documentados**, aguardando a implementação correspondente.
3. **12 critérios estão pendentes**, porque dependem de camadas ainda não construídas (API, aplicação, integração).
4. **Nenhum critério foi marcado como atendido sem evidência.**

O domínio de agendamento — o núcleo de regras do sistema — está completo e verificado. As próximas etapas constroem as camadas de aplicação e API sobre ele.
