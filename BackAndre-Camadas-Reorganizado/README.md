# BackAndre — arquitetura em camadas

Backend reorganizado para separar fisicamente as responsabilidades em projetos distintos, mantendo o fluxo solicitado:

```text
Controller -> Service -> Repository -> AppDbContext -> SQLite
```

## Estrutura

```text
BackAndre-Camadas-Reorganizado/
├── BackAndre.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Program.cs
│   ├── appsettings.json
│   └── BackAndre.Api.csproj
│
├── BackAndre.Application/
│   ├── DTOs/
│   ├── Services/
│   └── BackAndre.Application.csproj
│
├── BackAndre.Domain/
│   ├── Models/
│   └── BackAndre.Domain.csproj
│
├── BackAndre.Infrastructure/
│   ├── Data/
│   ├── Repositories/
│   ├── Migrations/
│   ├── Security/
│   └── BackAndre.Infrastructure.csproj
│
├── BackAndre.Tests/
│   └── BackAndre.Tests.csproj
│
└── BackAndre.sln
```

## Responsabilidade de cada camada

### BackAndre.Api
Entrada HTTP da aplicação.

- Controllers
- Middleware
- configuração da aplicação
- autenticação/autorização
- CORS
- Swagger
- Dependency Injection

### BackAndre.Application
Casos de uso e coordenação da aplicação.

- Services
- DTOs

Os Services usam os Repositories e chamam os comportamentos das entidades quando necessário.

### BackAndre.Domain
Núcleo do domínio.

- User
- Service
- Barber
- BarberSchedule
- Appointment
- DomainRuleException

Regras que pertencem ao próprio objeto continuam no domínio, como `Cancel`, `Reschedule`, `Complete`, `WorksOn` e `FitsWithinSchedule`.

### BackAndre.Infrastructure
Acesso a recursos externos e persistência.

- AppDbContext
- DbInitializer
- Repositories
- migrations do Entity Framework Core
- geração de JWT

### BackAndre.Tests
Testes automatizados do domínio.

## Observação sobre interfaces

Esta versão foi montada conforme solicitado, **sem interfaces de Service ou Repository**.

Por isso `BackAndre.Application` referencia diretamente `BackAndre.Infrastructure` para utilizar os Repositories concretos. É uma arquitetura em camadas simples e adequada ao objetivo acadêmico de demonstrar:

```text
Controller -> Service -> Repository
```

Não é uma implementação de Clean Architecture estrita.

## Comandos de validação

A partir da pasta do backend:

```cmd
dotnet restore BackAndre.sln
dotnet build BackAndre.sln
dotnet test BackAndre.Tests\BackAndre.Tests.csproj
```

O estado anterior do projeto possuía 15 testes. O esperado após a reorganização é continuar com:

```text
15 aprovados
0 falhas
```

## Executar a API

```cmd
dotnet run --project BackAndre.Api\BackAndre.Api.csproj
```

Swagger em desenvolvimento:

```text
http://localhost:5071/swagger
```

## Entity Framework Core

Como `AppDbContext` e as migrations agora estão no projeto Infrastructure, novos comandos do EF devem indicar o projeto de migrations e o projeto de inicialização.

Exemplo:

```cmd
dotnet ef migrations list --project BackAndre.Infrastructure\BackAndre.Infrastructure.csproj --startup-project BackAndre.Api\BackAndre.Api.csproj
```

Nenhuma migration nova foi criada nesta reorganização. As migrations existentes apenas foram movidas para `BackAndre.Infrastructure` e tiveram os namespaces ajustados.

## O que foi preservado

- endpoints existentes
- DTOs e formatos das respostas
- JWT e roles
- CORS
- SQLite
- migrations existentes
- regras do domínio
- seed de demonstração
- middleware de exceções
- testes existentes

O frontend não faz parte deste pacote e não precisa mudar apenas por causa dessa reorganização.

## Antes de fazer merge

Execute `dotnet build`, `dotnet test` e valide os endpoints pelo Swagger. Só depois faça commit/push da branch de refatoração e abra o PR para a `main`.
