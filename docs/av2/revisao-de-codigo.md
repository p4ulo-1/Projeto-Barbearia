# Revisão de Código — Endpoint de criação de agendamento

> **Finalidade:** atender aos critérios **C01–C06** — alteração funcional identificável, correspondência com requisito, inventário de arquivos, análise de impacto, avaliação por propriedades de POO e parecer técnico.
> **Alteração revisada:** commit `8a6843d` — *feat(api): implementa o endpoint de criação de agendamento (fluxo F1)*.
> **Método:** cada seção separa **fatos** (o que o código faz) de **julgamento** (o que isso significa).

---

## C01 — Ponto de comparação e diff verificável

**Ponto de comparação:** `6730f61` (commit anterior, com persistência implementada).
**Alteração:** `8a6843d`.

**Como reproduzir o diff:**

```bash
git diff 6730f61 8a6843d
```

**Resumo do diff:**

| Arquivo | Linhas | Natureza |
| --- | --- | --- |
| `src/Servia.Api/Controllers/AppointmentsController.cs` | +62 | Novo |
| `src/Servia.Api/Contracts/CreateAppointmentRequest.cs` | +19 | Novo |
| `src/Servia.Api/Contracts/AppointmentResponse.cs` | +41 | Novo |
| `src/Servia.Api/Program.cs` | +26 | Modificado |
| `src/Servia.Api/Servia.Api.csproj` | +1 | Modificado |
| `src/Servia.Tests/Api/AppointmentsEndpointTests.cs` | +196 | Novo |
| `docs/av2/auditoria-final.md` | ±10 | Modificado |
| `docs/av2/evidencias.json` | ±13 | Modificado |

**Total:** 357 inserções, 11 remoções.

---

## C02 — Correspondência com o requisito

**Requisito:** fluxo **F1 — Criar agendamento**, definido em `docs/av2/especificacao.md` §2.

**Fluxo especificado:**

1. Verificar se o profissional trabalha na data.
2. Verificar se o horário comporta a duração dentro da jornada.
3. Verificar se não há sobreposição com outro agendamento ativo.
4. Criar o agendamento no estado `Confirmed`.

**Onde cada passo foi implementado:**

| Passo | Implementação | Verificação |
| --- | --- | --- |
| 1 | `WorkSchedule.WorksOn` (domínio) | `WorkScheduleTests.WorksOn_Retorna_Falso_Em_Dia_Nao_Trabalhado` |
| 2 | `WorkSchedule.FitsWithinSchedule` (domínio) | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim` |
| 3 | `AppointmentService.HasOverlap` (aplicação) | `AppointmentServiceTests.Criar_Rejeita_Sobreposicao_De_Horario` |
| 4 | `Appointment.Create` (domínio) | `AppointmentTests.Criar_Agendamento_Valido_Inicia_Confirmado` |

**Correspondência com os critérios de aceitação:**

| Critério | Teste | Resultado |
| --- | --- | --- |
| F1-CA1 | `AppointmentsEndpointTests.Criar_Agendamento_Retorna_201_E_Persiste` | Passa |
| F1-CA2 | `WorkScheduleTests.WorksOn_Retorna_Falso_Em_Dia_Nao_Trabalhado` | Passa |
| F1-CA3 | `WorkScheduleTests.FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim` | Passa |
| F1-CA4 | `AppointmentsEndpointTests.Criar_Agendamento_Conflitante_Retorna_409` | Passa |

**Fato:** o código implementa o comportamento especificado, e cada critério tem teste correspondente que passa.

---

## C03 — Inventário dos arquivos e mudanças fora do escopo

### Finalidade de cada arquivo

| Arquivo | Finalidade |
| --- | --- |
| `AppointmentsController` | Recebe o pedido HTTP, delega ao caso de uso, traduz o resultado em status code |
| `CreateAppointmentRequest` | Contrato de entrada; isola a API do modelo de domínio |
| `AppointmentResponse` | Contrato de saída; expõe apenas o necessário |
| `Program.cs` | Composição: registra contexto, repositório, tempo e caso de uso |
| `Servia.Api.csproj` | Adiciona o pacote SQLite, necessário para `UseSqlite` |
| `AppointmentsEndpointTests` | Teste de integração ponta a ponta |
| `auditoria-final.md`, `evidencias.json` | Atualização do estado da avaliação |

### Mudanças fora do escopo

**Nenhuma alteração de domínio.** O commit não tocou em `Servia.Domain` nem em `Servia.Application`. Isso é correto: o fluxo F1 já estava implementado nas camadas internas; o commit apenas o expôs por HTTP.

**Uma mudança que merece atenção:** `Program.cs` passou a criar o schema com `EnsureCreatedAsync()` em desenvolvimento. Isso é aceitável em desenvolvimento, mas **não** é o mecanismo de produção — o próprio comentário no código registra que produção usará migrations. Registrado como pendência.

**Nenhum arquivo não relacionado foi alterado.** Não há mudanças de formatação, renomeações ou refatorações oportunistas.

---

## C04 — Análise de impacto com verificação

### Impacto local

| Componente | Impacto |
| --- | --- |
| `AppointmentsController` | Novo; não afeta código existente |
| `Program.cs` | Adiciona registros de DI; não remove nenhum |
| `Servia.Api.csproj` | Adiciona dependência; não altera versões existentes |

### Impacto transitivo

| Ponto | Análise |
| --- | --- |
| Contratos públicos da API | **Novo** endpoint; nenhum contrato existente foi alterado |
| `AppointmentService` | Passou a ser resolvido por DI; sua assinatura não mudou |
| `IAppointmentRepository` | Registrado pela interface; a implementação continua substituível |
| Camadas internas | **Zero alterações** — domínio e aplicação intactos |

### Contratos que merecem atenção

**`CreateAppointmentRequest` é um contrato público.** Uma vez publicado, alterá-lo quebra clientes. O campo `DurationMinutes` é recebido do cliente, embora o comentário diga que "a duração vem do serviço".

**Fato:** o comentário e o código divergem. O DTO recebe `DurationMinutes` do cliente.
**Julgamento:** é uma inconsistência de documentação, não de comportamento. O campo é necessário porque o serviço ainda não é consultado no caso de uso. Registrado como pendência.

### Verificação executada

| Verificação | Comando | Resultado |
| --- | --- | --- |
| Build | `build.cmd build Servia.slnx` | 0 erros, 0 avisos |
| Testes | `build.cmd test Servia.slnx` | 123 passando, 0 falhas |
| Regra de dependência | inspeção do `.csproj` | `Application` não referencia `Infrastructure` |

---

## C05 — Avaliação por propriedades de POO

### Encapsulamento

**Fato:** o controller não acessa estado interno do agregado. Ele recebe um DTO, monta um comando e lê o resultado.

**Fato:** `AppointmentResponse.From` lê apenas propriedades públicas de `Appointment`. Nenhum setter é usado.

**Julgamento:** o encapsulamento foi preservado. A API não consegue colocar o agregado em estado inválido, porque não tem acesso a setters.

### Coesão

**Fato:** o controller tem uma única responsabilidade — traduzir HTTP em chamada de caso de uso e o resultado em resposta. Não contém regra de negócio, não contém `try/catch`, não acessa o banco.

**Julgamento:** coesão alta. Se a regra de conflito mudar, o controller não muda.

### Acoplamento

**Fato:** o controller depende de `AppointmentService` (aplicação) e de dois DTOs. Não depende de `AppDbContext`, de repositório nem de `Infrastructure`.

**Fato:** `Program.cs` é o único ponto que conhece as implementações concretas.

**Julgamento:** acoplamento correto. A API depende de abstrações da aplicação; a composição fica isolada no ponto de entrada.

### Responsabilidades

**Fato:** a regra "não pode haver sobreposição" continua em `AppointmentService`, não no controller.

**Julgamento:** a responsabilidade permanece na camada correta. O controller não duplica a regra.

### Duplicação por tipo concreto

**Fato:** não há `if` verificando tipo concreto nem `switch` sobre tipo de entidade.

**Julgamento:** não há duplicação por tipo concreto.

---

## C06 — Parecer técnico

### Fatos

1. O código implementa o fluxo F1 conforme especificado.
2. Os quatro critérios de aceitação têm teste correspondente, e todos passam.
3. Nenhuma camada interna foi alterada.
4. Build limpo; 123 testes passando.
5. O controller não contém regra de negócio nem tratamento de exceção.

### Julgamento

**Decisão: ACEITAR com ressalvas.**

**Fundamentos para aceitar:**

- O requisito está implementado e verificado.
- As propriedades de POO avaliadas foram preservadas: encapsulamento, coesão, acoplamento e responsabilidades.
- Não há mudanças fora do escopo nem duplicação por tipo concreto.
- A separação de camadas foi mantida: a API não conhece persistência.

**Ressalvas registradas:**

| # | Ressalva | Impacto | Ação |
| --- | --- | --- | --- |
| R1 | `DurationMinutes` vem do cliente, mas o comentário diz que vem do serviço | Documentação inconsistente | Corrigir o comentário ou consultar o serviço no caso de uso |
| R2 | `EnsureCreatedAsync` em desenvolvimento | Não é o mecanismo de produção | Substituir por migrations antes do deploy |
| R3 | Não há endpoint de leitura por id | `Location` aponta para recurso inexistente | Implementar `GET /api/appointments/{id}` |

**Nenhuma ressalva impede a aceitação.** Todas estão registradas como pendências e não foram declaradas como resolvidas.

### Sequência de inspeção aplicada

1. **Definir o ponto de comparação** — `6730f61` → `8a6843d`.
2. **Inventariar os arquivos** — 8 arquivos, 357 inserções.
3. **Ler o diff orientado por requisito** — cada passo do F1 mapeado ao código.
4. **Verificar impacto** — contratos, dependências, camadas.
5. **Avaliar propriedades de POO** — encapsulamento, coesão, acoplamento, responsabilidades.
6. **Emitir parecer** — separando fatos de julgamento.
