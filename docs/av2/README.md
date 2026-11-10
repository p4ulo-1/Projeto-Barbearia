# Documentação AV2 — Design e Programação Orientados a Objetos

> **Projeto:** Servia — plataforma SaaS de gestão e atendimento para negócios de serviços.
> **Decisão vigente:** o Servia é um **sistema novo** (ADR-001). O código acadêmico em `backend/` e `frontend/` não é reaproveitado.
> **Situação:** a especificação está pronta; a implementação ainda não começou.

---

## 1. Como navegar

| Documento | Finalidade | Critérios |
| --- | --- | --- |
| [`especificacao.md`](especificacao.md) | Três fluxos, cinco regras, estados, situações de falha e critérios de aceitação | G01, A03, A04, A05, A06, D01 |
| [`modelo-de-classes.md`](modelo-de-classes.md) | Classes, atributos, métodos, associações, composição, contrato polimórfico e invariantes | B01–B07, A01, A02, A07, A08 |
| [`analise-do-modelo.md`](analise-do-modelo.md) | Duas decisões de modelagem comparando alternativas | B08 |
| [`registro-de-depuracao.md`](registro-de-depuracao.md) | Defeito, hipóteses, técnica, causa, correção e comparação | E01–E06 |
| [`rastreabilidade.md`](rastreabilidade.md) | Matriz fluxo → regra → implementação → teste | F01, F05 |
| [`auditoria-final.md`](auditoria-final.md) | O que está documentado, o que está pendente e o que está bloqueado | F06 |
| [`evidencias.json`](evidencias.json) | Índice legível por máquina dos 40 critérios | Todos |

---

## 2. Estado real da avaliação

| Estado | Quantidade | Significado |
| --- | --- | --- |
| Documentado | 20 | Especificação pronta; implementação pendente |
| Pendente | 19 | Exige código executável que ainda não existe |
| Bloqueado | 1 | F04 — ambiente .NET não compila |
| **Comprovado** | **0** | Nenhum critério tem evidência de execução |

**Nenhum critério foi declarado atendido sem evidência.** A auditoria final detalha cada um.

---

## 3. Bloqueio de ambiente

`dotnet restore` falha nesta máquina com `Value cannot be null. (Parameter 'path1')`.

**Causa:** a variável `APPDATA` não existe no Windows desta máquina. O NuGet depende dela para resolver a configuração.

**Como desbloquear:**

```powershell
setx APPDATA "C:\Users\Gabriel\AppData\Roaming"
```

Depois, reiniciar o terminal e executar:

```bash
cd backend
dotnet build
dotnet test
```

---

## 4. Próximos passos para a nota máxima

1. **Corrigir o ambiente** (`APPDATA`) — desbloqueia F04.
2. **Implementar o domínio** conforme `modelo-de-classes.md` — desbloqueia A01–A08 e B02–B07.
3. **Criar o projeto de testes** com os nomes exatos de `rastreabilidade.md` — desbloqueia F01–F03.
4. **Produzir uma alteração funcional real** com diff e análise — desbloqueia C01–C06 e G02.
5. **Executar a depuração** em código real — comprova E04.
6. **Atualizar `evidencias.json`** com os estados comprovados.

---

## 5. Princípio de honestidade

Este conjunto de documentos segue uma regra: **não declarar como concluído aquilo que não foi comprovado**. Onde a implementação não existe, o estado é "pendente". Onde o ambiente impede a verificação, o estado é "bloqueado". Onde há apenas especificação, o estado é "documentado".

Isso é intencional e atende ao critério F06.
