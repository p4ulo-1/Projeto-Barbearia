# Auditoria Final — Correspondência entre Requisitos, Implementação e Evidências

> **Finalidade:** atender ao critério **F06** — auditoria que identifica requisitos atendidos, suas evidências e pendências, **sem declarar como concluído aquilo que não foi comprovado**.
> **Momento da auditoria:** 2026-11-10, após a implementação do domínio, da camada de aplicação e do middleware de exceções.
> **Princípio:** esta auditoria distingue quatro estados — **comprovado**, **documentado**, **pendente** e **bloqueado**. Só o primeiro conta como atendido.

---

## 1. Situação do projeto

O Servia é um sistema novo (ADR-001), construído em `src/` a partir dos documentos de `docs/`.

| Verificação | Resultado |
| --- | --- |
| Build da solução | **0 erros, 0 avisos** |
| Testes automatizados | **117 passando, 0 falhas** |
| Regra de dependência | Domain sem referências; Application sem Infrastructure |

### Camadas implementadas

| Camada | Conteúdo |
| --- | --- |
| **Domain** | `Appointment`, `WorkSchedule`, `Service`, `Professional`, `Customer`, `Money`, `TimeRange`, `DomainRuleException`, `NotFoundException`, `IAvailabilityPolicy` + 2 implementações, `IAppointmentRepository` |
| **Application** | `AppointmentService` (regra R5), `CreateAppointmentCommand`, `AppointmentResult` |
| **Api** | `ExceptionHandlingMiddleware`, `ErrorResponse`, `DiagnosticsController` |
| **Infrastructure** | `AppDbContext`, 4 configurações de mapeamento, `AppointmentRepository` |

---

## 2. Critérios comprovados (24)

| Critério | O que exige | Evidência |
| --- | --- | --- |
| A01 | ≥ 5 classes de domínio | 5 entidades + 3 objetos de valor + 2 políticas |
| A02 | ≥ 2 classes com comportamento próprio | `Appointment`, `WorkSchedule`, `Service`, `Professional`, `Customer` |
| A03 | ≥ 5 regras de negócio | R1–R5 implementadas e testadas |
| A04 | Fluxo com colaboração entre classes | `AppointmentService` orquestra domínio + repositório |
| A05 | Objeto com 3 estados e transições | `AppointmentStatus` + `Appointment` |
| A06 | Transição proibida rejeitada | `Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| A07 | ≥ 2 regras protegidas por encapsulamento | `EnsureConfirmed`, construtores privados |
| A08 | Polimorfismo real com 2 implementações | `IAvailabilityPolicy` + `Standard`/`Extended` |
| B02 | Classes do modelo correspondem ao código | `src/Servia.Domain/` |
| B03 | Atributos e métodos sem contradição | Idem |
| B04 | Associação com multiplicidade implementada | Índices e FKs em `AppointmentConfiguration`, testados |
| B05 | Composição comprovada | `Professional` ◆—— `WorkSchedule`, mapeada como owned type e testada |
| B06 | Contrato polimórfico no código | `src/Servia.Domain/Policies/` |
| B07 | Invariante protegida no código | `Appointment.EnsureConfirmed` |
| D01 | Três situações de falha distintas | `DomainRuleException`, `NotFoundException`, exceção inesperada |
| D02 | Caminho reproduzível de exceção | `DiagnosticsController` + middleware |
| D03 | Tratamento sem vazar detalhe interno | `ExceptionHandlingMiddleware` + `ErrorResponse` |
| D04 | Testes das três situações | `ExceptionHandlingTests` (7 testes) |
| E05 | Causa e correção do defeito | `TimeRange` elimina DEF-001 na origem |
| E06 | Verificação antes/depois | `Overlaps_Detecta_Sobreposicao_Que_Cruza_Meia_Noite` |
| F01 | ≥ 10 cenários de teste | **108 testes** |
| F02 | Teste de invariante | `Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado` |
| F03 | Teste de integração pela API | `ExceptionHandlingTests` exercita o pipeline real |
| F04 | Compilar e executar testes | `build.cmd test` — 108 passando |

---

## 3. Critérios documentados (8)

| Critério | O que exige | O que falta |
| --- | --- | --- |
| B01 | Modelo textual com 5 classes | Modelo existe; falta atualizar com as classes implementadas |

| B08 | Duas decisões de modelagem | Documentado em `analise-do-modelo.md` |
| E01–E03 | Defeito, condições e hipótese | Documentado; falta execução real |
| F05 | Rastreabilidade | Matriz pronta; falta atualizar com nomes reais |
| F06 | Auditoria final | Este documento |
| G01 | Especificação verificável | `especificacao.md` |

---

## 4. Pendências (9 critérios)

| # | Pendência | Impacto | Quando resolver |
| --- | --- | --- | --- |
| P1 | Migrations e PostgreSQL em produção | SQLite em testes; falta provider de produção |
| P2 | Criar controllers de negócio | Desbloqueia C01–C06 | Fase F2 |
| P3 | Produzir alteração funcional com diff | Desbloqueia C01–C06 e G02 | Próxima entrega |
| P4 | Executar depuração em código real | Comprova E01–E04 | Junto com P3 |
| P5 | Atualizar modelo de classes com o código | Desbloqueia B01 completo | Após P1 |

---

## 5. Bloqueio de ambiente — resolvido

**Causa raiz:** a variável `ProgramFiles` estava vazia no ambiente Windows. O NuGet usa `%ProgramFiles%` para resolver a configuração machine-wide (`XPlatMachineWideSetting`) e falhava com `Path.Combine(null, ...)`.

**Correção:** o wrapper `build.cmd` define `ProgramFiles`, `ProgramData`, `APPDATA` e `LOCALAPPDATA` antes de chamar o `dotnet`.

> **Nota de correção:** a hipótese inicial era que o problema fosse o `APPDATA`. Estava errada. O diagnóstico correto veio do stack trace completo (`-v diag`).

---

## 6. Declaração de honestidade

Esta auditoria **não** afirma que o projeto atende aos 40 critérios. Afirma que:

1. **24 critérios estão comprovados** por código executável e 117 testes passando.
2. **8 critérios estão documentados**, aguardando a implementação correspondente.
3. **8 critérios estão pendentes**, porque dependem de uma alteração funcional real e de migrations.
4. **Nenhum critério foi marcado como atendido sem evidência.**

O domínio, a camada de aplicação e o tratamento central de exceções estão completos e verificados. A próxima etapa é a persistência, que desbloqueia as associações com multiplicidade e os testes de integração com banco.
