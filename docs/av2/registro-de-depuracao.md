# Registro de Depuração — Agendamento que ultrapassa a meia-noite

> **Finalidade:** atender aos critérios **E01–E06** — defeito reproduzível, condições de reprodução, hipótese e evidência, técnica de depuração aplicada, causa raiz, correção e comparação de técnicas.
> **Defeito:** `DEF-002` — agendamentos que terminam após a meia-noite são aceitos e não conflitam com o dia seguinte.
> **Natureza:** defeito **real**, encontrado no código implementado do Servia, reproduzido por teste automatizado.

---

## E01 — Defeito reproduzível

**Identificador:** `DEF-002`
**Localização:** `src/Servia.Domain/Common/TimeRange.cs`, método `From`.
**Descoberta:** por inspeção do código durante a busca por defeitos reais.

**Descrição:** `TimeRange.From` calcula `EndMinutes = StartMinutes + durationMinutes` sem verificar se o resultado ultrapassa o fim do dia (1440 minutos). Um agendamento às 23:50 com duração de 60 minutos produz `EndMinutes = 1490`, que representa 00:50 do dia seguinte — mas o agendamento continua pertencendo ao dia anterior.

**Evidência de reprodução:** `src/Servia.Tests/Repro/DefectReproTests.cs`

```bash
build.cmd test Servia.Tests --filter FullyQualifiedName~DefectReproTests
# Passed! - Failed: 0, Passed: 2
```

Os dois testes passam porque documentam o comportamento **atual**, que é incorreto.

---

## E02 — Condições de reprodução, esperado e observado

### Cenário 1 — agendamento aceito fora do dia

| Elemento | Valor |
| --- | --- |
| Data | 2026-11-09 |
| Horário | 23:50 |
| Duração | 60 minutos |

**Comportamento esperado:** o agendamento é **rejeitado**, porque termina às 00:50 do dia seguinte e não cabe no dia 09.

**Comportamento observado:** o agendamento é **aceito**. `OccupiedRange.EndMinutes` retorna `1490`, valor maior que `TimeRange.MinutesPerDay` (1440).

**Teste:** `DEF002_Agendamento_Que_Ultrapassa_Meia_Noite_Eh_Aceito`

### Cenário 2 — conflito real não detectado

| Agendamento | Data | Horário | Duração | Ocupa na prática |
| --- | --- | --- | --- | --- |
| A | 2026-11-09 | 23:50 | 60 min | 23:50 do dia 09 até 00:50 do dia 10 |
| B | 2026-11-10 | 00:30 | 30 min | 00:30 até 01:00 do dia 10 |

**Comportamento esperado:** os dois agendamentos se **sobrepõem** no instante real (00:30–00:50 do dia 10), então o segundo deveria ser rejeitado.

**Comportamento observado:** `A.OccupiedRange.Overlaps(B.OccupiedRange)` retorna **falso**. A comparação usa minutos do dia (1430–1490 contra 30–60) e ignora que pertencem a datas diferentes. O conflito **não é detectado**.

**Teste:** `DEF002_Dois_Agendamentos_Em_Dias_Diferentes_Conflitam_Indevidamente`

---

## E03 — Hipótese e evidência

**Hipótese H1:** `TimeRange` representa um intervalo dentro de **um único dia**, mas `From` não impõe essa invariante. Quando a duração ultrapassa o fim do dia, o valor de `EndMinutes` deixa de ter significado temporal — passa a ser um número maior que 1440 que não corresponde a nenhum horário válido.

**Como confirmar ou descartar:** verificar se `From` valida o limite superior e se `Overlaps` considera a data.

**Evidência coletada:**

| Verificação | Resultado |
| --- | --- |
| `TimeRange.From(23:50, 60).EndMinutes` | `1490` — maior que 1440 |
| `TimeRange.From` valida limite superior? | **Não** — só valida `durationMinutes > 0` |
| `Overlaps` compara datas? | **Não** — compara apenas minutos do dia |
| `Appointment.Create` valida o término? | **Não** — só valida duração positiva |

**Conclusão:** H1 **confirmada**. A causa é a ausência da invariante "o intervalo termina no mesmo dia" em `TimeRange.From`.

**Hipóteses descartadas:**

- *H2: o problema estaria em `WorkSchedule.FitsWithinSchedule`.* Descartada: esse método usa `IsContainedIn`, que compara com o expediente e rejeita corretamente horários fora da jornada. O defeito só aparece quando **não** há verificação de jornada — como em `Appointment.Create` direto.
- *H3: o problema estaria no fuso horário.* Descartada: `TimeRange` trabalha com minutos inteiros, sem fuso.

---

## E04 — Técnica de depuração aplicada

**Técnica principal: teste de reprodução com inspeção de valores intermediários.**

O defeito está em um cálculo puro, sem dependência externa. A técnica mais direta foi escrever um teste que exercita o cálculo com os valores do cenário e inspeciona o valor intermediário (`EndMinutes`), tornando visível o número que a lógica produz.

**Evidência da técnica:**

| Passo | Ação | Resultado observado |
| --- | --- | --- |
| 1 | Criar agendamento às 23:50 com 60 min | Aceito, sem exceção |
| 2 | Inspecionar `OccupiedRange.EndMinutes` | `1490` |
| 3 | Comparar com `TimeRange.MinutesPerDay` | `1490 > 1440` — fora do dia |
| 4 | Comparar dois agendamentos de dias diferentes | `Overlaps` retorna `false` |

**Técnica complementar: inspeção do código-fonte.** A leitura de `TimeRange.From` confirmou que não há validação de limite superior — o que explica o valor observado.

**Por que não usei breakpoint:** o defeito é determinístico e puro. Um teste que imprime o valor intermediário dá a mesma informação com menos cerimônia e deixa a evidência permanente no repositório.

---

## E05 — Causa raiz e correção

**Causa raiz:** `TimeRange` foi desenhado para representar um intervalo **dentro de um dia**, mas `From` não impõe essa invariante. A causa **não** é a localização do código, mas a ausência da validação que dá sentido ao tipo.

**Correção aplicada:** a regra não é global — depende do segmento. Foi introduzida a política `IOvernightPolicy` (ADR-018), com duas implementações:

- `NoOvernightPolicy` — padrão. Proíbe atendimento que termina após a meia-noite. Usada por barbearias, salões e estúdios de unhas.
- `AllowOvernightPolicy` — permite, com limite de 4 horas após a meia-noite. Usada por tatuagem, massoterapia e tranças.

`Appointment.Create` recebe a política como parâmetro opcional. Sem política explícita, aplica `NoOvernightPolicy` — o padrão é restritivo, nunca permissivo por omissão.

```csharp
var policy = overnightPolicy ?? new NoOvernightPolicy();
policy.EnsureAllowed(TimeRange.From(time, durationMinutes));
```

```csharp
public static TimeRange From(TimeOnly start, int durationMinutes)
{
    if (durationMinutes <= 0)
    {
        throw new DomainRuleException("A duração deve ser maior que zero.");
    }

    var startMinutes = ToMinutes(start);
    var endMinutes = startMinutes + durationMinutes;

    if (endMinutes > MinutesPerDay)
    {
        throw new DomainRuleException(
            "O atendimento não pode terminar após a meia-noite.");
    }

    return new TimeRange(startMinutes, endMinutes);
}
```

**Por que esta é a correção coerente:** o defeito não era a ausência de validação, mas a ausência de uma **regra explícita**. Proibir globalmente resolveria o caso da barbearia e quebraria o da tatuagem. A política torna a regra explícita, configurável por segmento e testável isoladamente.

**Alternativas descartadas:**

- *Proibir globalmente em `TimeRange.From`.* Descartada: quebraria segmentos com sessões longas e contradiria o requisito R5-CA4.
- *Booleano `AllowsOvernight` no agendamento.* Descartada: a variação é de comportamento, não de dado. Um booleano exigiria condicionais no núcleo (ADR-018).
- *Validar apenas em `Appointment.Create`.* Descartada: seria remédio local, sem contrato explícito nem testabilidade isolada.

---

## E06 — Verificação antes e depois, e comparação de técnicas

### Verificação antes e depois

| Cenário | Antes | Política padrão | Política de sessão longa |
| --- | --- | --- | --- |
| 23:50 + 60 min | Aceito — **incorreto** | Rejeitado — correto | Aceito — correto |
| 23:50 + 10 min (termina 24:00) | Aceito | Aceito — correto | Aceito — correto |
| 10:00 + 60 min | Aceito | Aceito — correto | Aceito — correto |
| 22:00 + 8h | Aceito — **incorreto** | Rejeitado — correto | Rejeitado — correto (limite de 4h) |

**Não regressão:** os cenários diurnos mantêm o comportamento em ambas as políticas. Os 130 testes passam após a correção.

### Comparação entre técnicas de depuração

| Critério | Teste de reprodução com inspeção | Breakpoint no depurador |
| --- | --- | --- |
| Adequação ao defeito | **Alta** — cálculo puro e determinístico | Alta, mas exige sessão interativa |
| Permanência da evidência | **Alta** — o teste fica no repositório | Nula — a sessão termina |
| Custo de preparação | Baixo — um teste | Médio — configurar e anexar o depurador |
| Precisão da localização | **Alta** — aponta o valor exato | **Alta** — para na linha exata |
| Repetibilidade | **Alta** — roda no pipeline | Baixa — manual |
| Aplicabilidade a defeitos de cálculo | **Alta** | Alta |

**Recomendação:** para defeitos de cálculo puro, o teste de reprodução é superior ao breakpoint porque deixa a evidência permanente e roda no pipeline. O breakpoint é mais eficaz para defeitos que dependem de estado complexo ou de fluxo entre camadas, onde a reprodução isolada é difícil.

**Técnica recomendada para o projeto:** teste de reprodução como padrão, com breakpoint reservado para investigação de fluxo entre camadas.

---

## Defeito anterior — DEF-001

O registro anterior descrevia o `DEF-001` (comparação de intervalos com `TimeOnly` cíclico) como **risco de desenho**, evitado na origem pelo `TimeRange`. Ele permanece documentado como decisão de projeto, não como defeito corrigido.

O `DEF-002` é o defeito **real** deste registro: existia no código, foi reproduzido e foi corrigido.
