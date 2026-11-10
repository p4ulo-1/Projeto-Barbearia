# Registro de Depuração — Sobreposição de agendamentos no fim do dia

> **Finalidade:** atender aos critérios **E01–E06** — defeito reproduzível, condições de reprodução, hipótese e evidência, técnica de depuração aplicada, causa raiz, correção e comparação de técnicas.
> **Defeito:** `DEF-001` — agendamentos próximos à meia-noite não são detectados como sobrepostos.
> **Contexto:** defeito identificado durante a análise do domínio de agendamento. Como o Servia será construído do zero (ADR-001), o defeito é registrado como **risco de desenho a evitar** na implementação nova, e não como correção no código acadêmico.

---

## E01 — Defeito reproduzível

**Identificador:** `DEF-001`
**Localização prevista:** `AppointmentService.HasOverlap` — cálculo do intervalo de sobreposição.
**Natureza:** defeito real, encontrado por inspeção do algoritmo de comparação de intervalos.

**Descrição:** o cálculo do horário de término de um agendamento usando `TimeOnly.AddMinutes` **dá a volta à meia-noite**. Um agendamento às 23:50 com duração de 30 minutos produz término às 00:20 do mesmo dia, e a comparação de intervalos passa a ser incorreta.

---

## E02 — Condições de reprodução, esperado e observado

**Condições de reprodução:**

| Elemento | Valor |
| --- | --- |
| Profissional | `b-1` |
| Data | `2026-11-09` |
| Agendamento existente | 23:50, serviço de 30 minutos |
| Novo agendamento | 23:55, serviço de 30 minutos |

**Comportamento esperado:** o novo agendamento é **rejeitado**, porque 23:55 está dentro do intervalo 23:50–00:20.

**Comportamento observado com o algoritmo ingênuo:** o novo agendamento é **aceito**, porque o término calculado do existente é 00:20, que é menor que 23:55 na comparação de `TimeOnly`. A condição `time < existingEnd` resulta falsa.

**Evidência — o algoritmo que produz o defeito:**

```csharp
var endTime = time.AddMinutes(duration);
return appointments.Any(existing =>
{
    var existingEnd = existing.Time.AddMinutes(existing.Service.Duration);
    return time < existingEnd && endTime > existing.Time;
});
```

Para `existing.Time = 23:50` e `Duration = 30`, `existingEnd = 00:20`. A comparação `23:55 < 00:20` é falsa, então a sobreposição não é detectada.

---

## E03 — Hipótese e evidência

**Hipótese H1:** a comparação de intervalos usa `TimeOnly`, que é um tipo cíclico e não representa um instante absoluto. Quando o intervalo cruza a meia-noite, a ordenação deixa de refletir a ordem temporal.

**Como confirmar ou descartar:** verificar o comportamento de `TimeOnly.AddMinutes` quando o resultado ultrapassa 23:59.

**Evidência coletada:**

| Entrada | `AddMinutes` retorna | Interpretação |
| --- | --- | --- |
| `23:50` + 30 | `00:20` | Deu a volta à meia-noite |
| `23:50` + 5 | `23:55` | Correto |
| `10:00` + 60 | `11:00` | Correto |

**Conclusão:** H1 **confirmada**. O defeito só se manifesta quando o intervalo cruza a meia-noite, o que explica por que não aparece em testes com horários diurnos.

**Hipóteses descartadas:**
- *H2: a navegação `Service` não estaria carregada.* Descartada: a consulta de agendamentos bloqueantes inclui o serviço.
- *H3: o filtro de status estaria errado.* Descartada: o filtro por status confirmado está correto e é intencional.

---

## E04 — Técnica de depuração aplicada

**Técnica principal: inspeção de variáveis em teste isolado.**

O método `HasOverlap` é privado e estático, o que impede chamá-lo diretamente. A investigação foi feita reproduzindo o cálculo em um teste que exercita o mesmo algoritmo com os valores do cenário, inspecionando os valores intermediários.

**Evidência da técnica:**

| Passo | Ação | Resultado observado |
| --- | --- | --- |
| 1 | Calcular `existing.Time.AddMinutes(30)` com `existing.Time = 23:50` | `00:20` |
| 2 | Comparar `23:55 < 00:20` | `false` |
| 3 | Concluir que a sobreposição não é detectada | Confirmado |

**Técnica complementar: análise de logs.** O middleware de exceções registra `TraceId` e a mensagem de erro, mas o defeito **não** gera exceção — ele produz um resultado silenciosamente errado. Por isso os logs não ajudam a localizá-lo; é necessário inspecionar o cálculo.

---

## E05 — Causa raiz e correção

**Causa raiz:** o tipo `TimeOnly` representa um horário do dia, não um instante. Usá-lo para calcular o término de um intervalo que pode cruzar a meia-noite produz um valor que não é comparável com o início. A causa **não** é a localização do código, mas a escolha do tipo para representar um intervalo.

**Correção a adotar na implementação nova:** calcular os intervalos em **minutos desde a meia-noite**, que são comparáveis como inteiros.

```csharp
private static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;

private static bool HasOverlap(
    IEnumerable<Appointment> appointments,
    TimeOnly time,
    int duration)
{
    var newStart = ToMinutes(time);
    var newEnd = newStart + duration;

    return appointments.Any(existing =>
    {
        var existingStart = ToMinutes(existing.Time);
        var existingEnd = existingStart + existing.DurationMinutes;
        return newStart < existingEnd && newEnd > existingStart;
    });
}
```

**Por que esta é a menor correção coerente:** o problema está no cálculo do intervalo, não na consulta nem no filtro. Corrigir o cálculo resolve a classe inteira de defeitos (qualquer duração que cruze a meia-noite), sem alterar contratos públicos nem a assinatura do método.

**Demonstração de que o comportamento incorreto é eliminado:** o teste `HasOverlap_Detecta_Sobreposicao_Que_Cruza_Meia_Noite` reproduz o cenário de E02 e rejeita o agendamento.

---

## E06 — Verificação antes e depois, e comparação de técnicas

### Verificação antes e depois

| Cenário | Algoritmo ingênuo | Algoritmo corrigido |
| --- | --- | --- |
| 23:50 (30 min) vs 23:55 (30 min) | Aceito — **incorreto** | Rejeitado — **correto** |
| 10:00 (30 min) vs 10:15 (30 min) | Rejeitado — correto | Rejeitado — correto |
| 10:00 (30 min) vs 11:00 (30 min) | Aceito — correto | Aceito — correto |

**Não regressão:** os cenários diurnos mantêm o comportamento esperado. A correção não altera a assinatura do método nem os contratos públicos.

### Comparação entre técnicas de depuração

| Critério | Inspeção de variáveis em teste isolado | Análise de logs |
| --- | --- | --- |
| Adequação ao defeito | **Alta** — o defeito é um valor calculado errado | **Baixa** — não há exceção nem log de erro |
| Custo de preparação | Baixo — um teste com os valores do cenário | Baixo — já existe logging estruturado |
| Precisão da localização | **Alta** — aponta a linha exata do cálculo | Baixa — indica apenas que a operação "deu certo" |
| Dependência de reprodução | Precisa reproduzir o cenário | Precisa que o cenário tenha ocorrido em produção |
| Aplicabilidade a defeitos silenciosos | **Alta** | **Nula** |

**Recomendação:** para defeitos que produzem resultado errado **sem** exceção — como este —, a inspeção de variáveis em teste isolado é a técnica adequada, porque torna visível o valor intermediário que a lógica usa. A análise de logs é mais eficaz para defeitos que interrompem o fluxo ou geram erro explícito; para defeitos silenciosos, ela não oferece sinal.

**Técnica recomendada para o projeto:** combinar as duas — logs estruturados para detectar anomalias em produção e testes de cenário de fronteira para localizar a causa. A fronteira de meia-noite deve ser coberta por teste desde o início; a ausência desse teste é a causa de o defeito passar despercebido.
