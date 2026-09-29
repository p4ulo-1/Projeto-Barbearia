# BackAndre — API da Barbearia New Age

Backend acadêmico para autenticação, clientes, serviços, barbeiros e agendamentos da New Age. O projeto usa práticas reais de mercado sem adicionar camadas desnecessárias.

## Tecnologias

- .NET 10 e ASP.NET Core Web API
- Entity Framework Core 10 e SQLite
- JWT Bearer Authentication e `PasswordHasher<User>`
- Swagger/OpenAPI
- xUnit

## Estrutura

```text
Controllers/       Endpoints HTTP e coordenação das operações
Data/              AppDbContext e dados iniciais
DTOs/              Contratos separados de entrada e saída
Middleware/        Tratamento centralizado de erros
Models/            Entidades e regras de domínio
Services/          Geração do JWT
Migrations/        Histórico do esquema SQLite
BackAndre.Tests/   Testes unitários das regras de domínio
```

Os controllers acessam diretamente o `AppDbContext`. Não há Repository Pattern, CQRS, MediatR, AutoMapper ou Unit of Work adicional.

## Restaurar e preparar

```powershell
dotnet tool restore
dotnet restore
dotnet restore BackAndre.Tests\BackAndre.Tests.csproj
```

## Migrations e SQLite

```powershell
dotnet tool run dotnet-ef migrations add NomeDaMigration
dotnet tool run dotnet-ef database update
```

O segundo comando cria ou atualiza `barbearia.db`. A API também chama `Database.MigrateAsync()` ao iniciar, e o `DbInitializer` inclui os dados demonstrativos quando as tabelas estão vazias.

## Compilar, testar e executar

```powershell
dotnet build
dotnet test BackAndre.Tests\BackAndre.Tests.csproj
dotnet run
```

- API: `http://localhost:5071`
- Swagger: `http://localhost:5071/swagger`

## Credenciais de demonstração

- Cliente: `cliente@exemplo.com` / `cliente123`
- Administrador: `admin@newagebarber.com.br` / `newage123`

As senhas são persistidas somente como hash.

## JWT e Bearer Token

1. Envie e-mail e senha para `POST /api/auth/login`.
2. O backend verifica o hash da senha.
3. A resposta contém usuário, expiração e JWT com ID e role.
4. No Swagger, clique em **Authorize** e informe `Bearer {token}`.
5. Os endpoints identificam o usuário pelas claims; IDs e roles enviados pelo cliente não são aceitos como identidade.

`[Authorize]` protege operações autenticadas e `[Authorize(Roles = "admin")]` protege operações administrativas.

## Configuração local do JWT

A chave de `appsettings.json` é apenas demonstrativa. Para sobrescrevê-la por variável de ambiente:

```powershell
$env:Jwt__Key = "uma-chave-local-longa-e-segura"
dotnet run
```

Ou por User Secrets:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "uma-chave-local-longa-e-segura"
```

Em produção, a chave deve vir de configuração protegida, nunca do repositório.

## Fluxo de agendamento

1. O cliente envia serviço, barbeiro, data e horário.
2. O controller valida autenticação, existência, atividade e disponibilidade no banco.
3. `BarberSchedule` verifica dia trabalhado e encaixe no expediente.
4. A agenda verifica sobreposição usando a duração completa dos serviços.
5. `Appointment` nasce confirmado e define internamente `CreatedAt`.
6. O EF Core persiste o agendamento no SQLite.

Remarcação, cancelamento e conclusão são coordenados pelo controller, mas executados pela entidade `Appointment`.

## Consulta de disponibilidade

```http
GET /api/appointments/availability?serviceId=combo-premium&barberId=rafael&date=2026-10-01
```

A resposta contém somente a data e os horários livres. Não expõe usuários nem dados dos agendamentos. A consulta considera serviço e barbeiro ativos, jornada, duração completa, conflitos e antecedência mínima.

Na remarcação, o parâmetro opcional `appointmentId` permite retirar o próprio agendamento do cálculo. Ele somente é aceito quando o JWT identifica o proprietário do agendamento ou um administrador; solicitações anônimas ou de outro cliente recebem `403 Forbidden`.

## Regras principais

- Serviço e barbeiro precisam existir e estar ativos.
- A data não pode estar no passado.
- Para hoje, exige-se antecedência mínima de 30 minutos.
- O barbeiro precisa trabalhar no dia escolhido.
- O serviço precisa caber integralmente no expediente.
- Conflitos consideram início, fim e duração completa.
- Cancelados e concluídos não bloqueiam horários.
- A remarcação ignora o próprio agendamento na consulta de conflito.
- Cliente acessa apenas seus próprios agendamentos.
- Admin pode listar todos, concluir e excluir.

## Estados de Appointment

- `confirmado`
- `cancelado`
- `concluido`

| Estado atual | Operação | Resultado |
|---|---|---|
| confirmado | cancelar | cancelado |
| confirmado | concluir, se não for futuro | concluido |
| confirmado | remarcar | permanece confirmado |
| cancelado | cancelar, concluir ou remarcar | rejeitado |
| concluido | cancelar, concluir ou remarcar | rejeitado |

`Status`, `Date`, `Time` e `CreatedAt` não possuem setter público. As transições passam por `Cancel()`, `Complete(...)` e `Reschedule(...)`.

## Testes automatizados

```powershell
dotnet test BackAndre.Tests\BackAndre.Tests.csproj
```

Os testes cobrem criação de agendamento, transições válidas e inválidas, dias trabalhados, expediente e construção inválida de jornada.

## Limitações conhecidas

- Não há refresh token ou revogação antecipada do JWT.
- Não há feriados, folgas específicas ou intervalo de almoço.
- A API usa a hora local do servidor nas regras de data e antecedência.
- O conflito é validado pela aplicação; requisições perfeitamente simultâneas ainda podem disputar um horário.
- Fotos dos barbeiros continuam sob responsabilidade do frontend.
