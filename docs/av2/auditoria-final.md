# Auditoria Final — Correspondência entre Requisitos, Implementação e Evidências

> **Finalidade:** atender ao critério **F06** — auditoria que identifica requisitos atendidos, suas evidências e pendências, **sem declarar como concluído aquilo que não foi comprovado**.
> **Momento da auditoria:** 2026-11-10, antes do início da implementação do Servia.
> **Princípio:** esta auditoria distingue três estados — **planejado**, **documentado** e **comprovado**. Só o último conta como atendido.

---

## 1. Situação do projeto

O Servia será construído do zero (ADR-001). O código acadêmico em `backend/` e `frontend/` **não** é base do sistema novo.

Consequência direta para esta auditoria: **nenhum critério que exija código executável está comprovado neste momento**, porque a implementação ainda não começou. O que existe é a especificação, o modelo e o plano de testes.

---

## 2. Critérios por estado

### 2.1. Documentados (especificação pronta, implementação pendente)

| Critério | O que exige | Evidência documental | Estado |
| --- | --- | --- | --- |
| A01 | ≥ 5 classes de domínio | `modelo-de-classes.md` §1 (5 classes) | Documentado |
| A02 | ≥ 2 classes com comportamento próprio | `modelo-de-classes.md` §1.1, §1.2 | Documentado |
| A03 | ≥ 5 regras de negócio | `especificacao.md` §3 (R1–R5) | Documentado |
| A04 | Fluxo com colaboração entre classes | `especificacao.md` §2 (F1) | Documentado |
| A05 | Objeto com 3 estados e transições | `especificacao.md` §4 | Documentado |
| A06 | Transição proibida rejeitada | `especificacao.md` §4 (E-CA1) | Documentado |
| A07 | ≥ 2 regras protegidas por encapsulamento | `modelo-de-classes.md` §1.1, §1.2 | Documentado |
| A08 | Polimorfismo real com 2 implementações | `modelo-de-classes.md` §4 | Documentado |
| B01–B07 | Modelo textual com classes, atributos, associações, composição, contrato e invariantes | `modelo-de-classes.md` | Documentado |
| B08 | Duas decisões de modelagem comparando alternativas | `analise-do-modelo.md` | Documentado |
| D01 | Três situações de falha distintas | `especificacao.md` §5 | Documentado |
| E01–E06 | Defeito, hipótese, técnica, causa, correção e comparação | `registro-de-depuracao.md` | Documentado |
| F01 | Testes de sucesso e violação para 5 regras | `rastreabilidade.md` §2 (16 cenários) | Documentado |
| F05 | Rastreabilidade fluxo → regra → implementação → teste | `rastreabilidade.md` | Documentado |
| G01 | Especificação verificável dos 3 fluxos e 5 regras | `especificacao.md` | Documentado |

### 2.2. Não comprovados (exigem código executável)

| Critério | O que exige | Por que não está comprovado |
| --- | --- | --- |
| B02 | Classes do modelo correspondem ao código C# | O código do Servia ainda não existe |
| B03 | Atributos e métodos do modelo sem contradição com o código | Idem |
| B04 | Associação com multiplicidade implementada | Idem |
| B05 | Composição comprovada na implementação | Idem |
| B06 | Contrato polimórfico e duas implementações no código | Idem |
| B07 | Invariante identificada e protegida no código | Idem |
| C01–C06 | Alteração funcional com diff, impacto e parecer | Exige histórico Git de uma alteração real no código novo |
| D02 | Caminho reproduzível de lançamento e tratamento de exceção | Exige código executável |
| D03 | Tratamento adequado sem vazar detalhe interno | Exige API em execução |
| D04 | Testes das três situações de falha | Exige projeto de testes |
| E04 | Técnica de depuração comprovada em execução | A técnica foi descrita; a execução depende do código |
| F02 | Teste de invariante confirmando estado válido | Exige projeto de testes |
| F03 | Teste de integração pela API com persistência | Exige API e banco |
| F04 | Compilar e executar testes a partir de clone limpo | **Bloqueado pelo ambiente** (ver §3) |
| G02 | Especificação para o DsCode com evidência de verificação | Exige uma alteração funcional executada |

---

## 3. Bloqueio de ambiente

**Fato observado:** `dotnet restore` falha nesta máquina com `Value cannot be null. (Parameter 'path1')` em `NuGet.Configuration.ConfigurationDefaults`.

**Causa identificada:** a variável de ambiente `APPDATA` não existe no Windows desta máquina. O NuGet depende dela para resolver o caminho de configuração.

**Evidência:** `cmd /c echo APPDATA=%APPDATA%` retorna a variável não expandida; `dotnet nuget list source` falha com o mesmo erro; um projeto novo criado em diretório temporário também falha.

**Consequência:** **não foi possível compilar nem executar testes** neste ambiente. O critério **F04** está bloqueado, e todos os critérios que dependem de execução permanecem não comprovados.

**O que NÃO foi feito:** não declarei F04 como atendido, não simulei resultados de teste e não alterei o projeto para contornar o problema de ambiente.

**Como desbloquear:** definir `APPDATA=C:\Users\Gabriel\AppData\Roaming` nas variáveis de ambiente do Windows e reiniciar o terminal. Em seguida, `dotnet build` e `dotnet test` passam a ser executáveis.

---

## 4. Pendências

| # | Pendência | Impacto | Quando resolver |
| --- | --- | --- | --- |
| P1 | Implementar o domínio de agendamento do zero | Desbloqueia A01–A08, B02–B07, D02–D04, F02–F03 | Fase F2 do Roadmap |
| P2 | Criar o projeto de testes do Servia | Desbloqueia F01–F03 | Junto com P1 |
| P3 | Corrigir o ambiente .NET (`APPDATA`) | Desbloqueia F04 e toda verificação executável | Antes da primeira entrega |
| P4 | Produzir uma alteração funcional real com diff | Desbloqueia C01–C06 e G02 | Primeira entrega de código |
| P5 | Executar a depuração em código real | Comprova E04 | Junto com P4 |

---

## 5. Declaração de honestidade

Esta auditoria **não** afirma que o projeto atende aos 40 critérios. Afirma que:

1. A **especificação** dos fluxos, regras, modelo, decisões e depuração está pronta e é verificável.
2. A **implementação** ainda não existe, porque o Servia será construído do zero.
3. A **verificação executável** está bloqueada por um problema de ambiente que não é do projeto.
4. Nenhum critério foi marcado como atendido sem evidência correspondente.

O caminho para a nota máxima está definido: implementar o domínio conforme a especificação, criar os testes nomeados na matriz de rastreabilidade, corrigir o ambiente e produzir as evidências de execução.
