# Análise do Modelo — Decisões de Modelagem

> **Finalidade:** atender ao critério **B08** — duas decisões de modelagem, cada uma comparando alternativas e justificando tecnicamente a escolha.
> **Escopo:** decisões tomadas no desenho do domínio de agendamento do Servia, que será construído do zero (ADR-001).
> **Método:** cada decisão parte de uma pergunta concreta, apresenta duas alternativas viáveis, compara-as por critérios objetivos e conclui com a escolha adotada.

---

## Decisão 1 — Onde proteger a regra de não sobreposição de agendamentos

### Pergunta

A regra "dois agendamentos do mesmo profissional não podem se sobrepor" depende de mais de um agendamento. Onde ela deve ser protegida?

### Alternativa A — Na entidade `Appointment`

`Appointment` receberia a lista de agendamentos existentes do profissional e verificaria a sobreposição em seu próprio construtor.

```csharp
public Appointment(..., IEnumerable<Appointment> existing) { /* verifica sobreposição */ }
```

**Vantagens:** a regra fica junto do dado que ela protege.
**Desvantagens:**
- A entidade passaria a depender de uma coleção externa, quebrando o princípio de que um agregado protege apenas o seu próprio estado.
- Toda criação precisaria carregar a agenda inteira do profissional, mesmo quando não houver conflito.
- O construtor ficaria com duas responsabilidades: validar o próprio objeto e consultar o conjunto.

### Alternativa B — No serviço de aplicação `AppointmentService`

`AppointmentService` consulta os agendamentos bloqueantes do profissional e verifica a sobreposição antes de criar.

```csharp
private static bool HasOverlap(...) { /* compara intervalos */ }
```

**Vantagens:**
- O agregado `Appointment` permanece responsável apenas pelo seu próprio estado.
- A consulta ao repositório fica na camada que já tem acesso a ele.
- A regra é testável isoladamente com um repositório dublê.

**Desvantagens:**
- A regra não está no domínio puro; depende de orquestração.
- Existe uma janela entre a verificação e a gravação (condição de corrida), mitigada pelo `ScheduleLock` com índice único parcial (ADR-017).

### Comparação

| Critério | Alternativa A | Alternativa B |
| --- | --- | --- |
| Coesão do agregado | Baixa — mistura estado próprio com consulta externa | Alta — cada objeto com uma responsabilidade |
| Acoplamento | `Appointment` passa a depender de coleção externa | `Appointment` permanece independente |
| Testabilidade | Exige montar a coleção inteira | Testável com dublê do repositório |
| Custo de consulta | Sempre carrega a agenda | Carrega apenas os bloqueantes |
| Proteção contra corrida | Nenhuma | `ScheduleLock` + índice único parcial |

### Decisão

**Alternativa B**, implementada em `AppointmentService.HasOverlap`.

**Justificativa:** a regra ultrapassa uma instância — não pode ser protegida por uma única entidade sem que ela passe a depender de dados externos. O serviço de aplicação é o ponto correto porque já orquestra repositório e domínio. A proteção contra concorrência fica no banco, onde é efetiva.

---

## Decisão 2 — Como representar a variação de disponibilidade entre profissionais

### Pergunta

Profissionais diferentes têm regras de disponibilidade diferentes: alguns exigem que o atendimento termine antes do fim do expediente, outros aceitam uma pequena tolerância. Como representar essa variação sem espalhar condicionais?

### Alternativa A — Um campo booleano em `WorkSchedule`

```csharp
public bool AllowsEndAtClosing { get; set; }
```

E `FitsWithinSchedule` consultaria o campo.

**Vantagens:** simples, sem novas classes.
**Desvantagens:**
- Cada nova variação exige um novo booleano, gerando combinações inválidas.
- O comportamento fica disperso em condicionais dentro de um método.
- Não há contrato explícito; a variação é invisível no modelo.

### Alternativa B — Contrato `IAvailabilityPolicy` com duas implementações

```csharp
public interface IAvailabilityPolicy
{
    bool IsAvailable(WorkSchedule schedule, DateOnly date, TimeOnly time, int durationMinutes);
    string Describe();
}
```

Com `StandardAvailabilityPolicy` e `ExtendedAvailabilityPolicy`.

**Vantagens:**
- A variação é explícita no modelo e no código.
- Nova variação = nova classe, sem tocar nas existentes (princípio aberto/fechado).
- Cada implementação é testável isoladamente.
- O serviço depende do contrato, não da implementação.

**Desvantagens:**
- Mais arquivos e uma indireção.
- Exige configuração de qual implementação usar.

### Comparação

| Critério | Alternativa A | Alternativa B |
| --- | --- | --- |
| Extensibilidade | Cada variação exige novo campo | Cada variação é uma nova classe |
| Legibilidade | Condicionais acumulam | Comportamento nomeado por classe |
| Polimorfismo | Nenhum | Real, com duas implementações |
| Testabilidade | Testa o método com combinações de flags | Testa cada política isoladamente |
| Acoplamento | `WorkSchedule` conhece todas as variações | `WorkSchedule` não conhece nenhuma |

### Decisão

**Alternativa B**, implementada em `BackAndre.Domain/Policies/`.

**Justificativa:** a variação é de **comportamento**, não de dado. Representá-la como dado (booleano) obriga o código a interpretar o dado em condicionais; representá-la como comportamento (polimorfismo) permite que cada variação seja uma classe com nome próprio. O critério A08 exige polimorfismo real com duas implementações efetivamente usadas — esta é a variação legítima do domínio, não uma abstração artificial.

---

## Roteiro de análise aplicado

As duas decisões seguiram o mesmo roteiro:

1. **Identificar a pergunta** — qual problema concreto o modelo precisa resolver.
2. **Levantar alternativas viáveis** — no mínimo duas, ambas plausíveis.
3. **Comparar por critérios objetivos** — coesão, acoplamento, testabilidade, extensibilidade.
4. **Decidir e justificar** — com referência ao modelo adotado.
5. **Registrar a consequência** — inclusive as desvantagens aceitas.

---

## Limitações reconhecidas

| # | Limitação | Impacto |
| --- | --- | --- |
| L1 | A verificação de sobreposição tem janela de corrida | Mitigada por `ScheduleLock`, não eliminada por transação explícita |
| L2 | `IAvailabilityPolicy` exige configuração por tenant | Um passo a mais na composição da aplicação |
| L3 | O modelo de agendamento cobre um serviço por agendamento | Combos e múltiplos profissionais exigem evolução futura |

Estas limitações estão registradas na auditoria final e **não** são declaradas como resolvidas.
