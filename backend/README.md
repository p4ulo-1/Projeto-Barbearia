# New Age Barbearia — Backend

Backend da aplicação **New Age Barbearia**, desenvolvido em C# com ASP.NET Core Web API.

O backend é responsável pela API HTTP, autenticação, autorização, regras de aplicação, regras de domínio, persistência e acesso aos dados.

---

## 🏗️ Arquitetura

O backend utiliza uma arquitetura em camadas com separação física das responsabilidades em projetos distintos.

Fluxo principal:

```text
Controller
    ↓
Service
    ↓
Repository
    ↓
AppDbContext
    ↓
SQLite
```

A organização foi definida para separar:

* entrada HTTP;
* casos de uso;
* regras de domínio;
* persistência;
* acesso a recursos externos;
* testes.

Esta é uma **arquitetura em camadas simples** voltada ao objetivo acadêmico do projeto. Ela não pretende ser uma implementação estrita de Clean Architecture.

---

## 📁 Estrutura

```text
backend/
│
├── BackAndre.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Program.cs
│   ├── Properties/
│   ├── appsettings.json
│   ├── appsettings.Development.json
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
│   ├── AppointmentTests.cs
│   ├── BarberScheduleTests.cs
│   └── BackAndre.Tests.csproj
│
├── ARQUITETURA.md
└── ...
```

---

## 🧩 Responsabilidade das camadas

### BackAndre.Api

É a porta de entrada HTTP da aplicação.

Responsabilidades:

* Controllers;
* middleware;
* configuração da aplicação;
* Dependency Injection;
* autenticação;
* autorização;
* CORS;
* Swagger / OpenAPI.

Controllers disponíveis:

* `AuthController`
* `UsersController`
* `ServicesController`
* `BarbersController`
* `AppointmentsController`

---

### BackAndre.Application

Contém os casos de uso e a coordenação das operações da aplicação.

Responsabilidades:

* Services;
* DTOs;
* validação e coordenação dos fluxos de aplicação.

Services principais:

* `AuthService`
* `UserService`
* `ServiceService`
* `BarberService`
* `AppointmentService`

Os Services utilizam os Repositories e acionam os comportamentos das entidades quando necessário.

---

### BackAndre.Domain

Representa o núcleo do domínio da aplicação.

Principais entidades:

* `User`
* `Service`
* `Barber`
* `BarberSchedule`
* `Appointment`

Também contém:

* `DomainRuleException`

As regras que pertencem às próprias entidades permanecem encapsuladas no domínio.

Exemplos:

* `Appointment.Cancel()`
* `Appointment.Reschedule()`
* `Appointment.Complete()`
* `BarberSchedule.WorksOn()`
* `BarberSchedule.FitsWithinSchedule()`

---

### BackAndre.Infrastructure

Responsável pelo acesso a recursos externos e pela persistência.

Contém:

* `AppDbContext`;
* `DbInitializer`;
* Repositories;
* migrations do Entity Framework Core;
* geração de tokens JWT.

Repositories:

* `UserRepository`
* `ServiceRepository`
* `BarberRepository`
* `AppointmentRepository`

---

### BackAndre.Tests

Contém os testes automatizados do backend.

Os testes atuais verificam principalmente regras do domínio relacionadas a:

* agendamentos;
* transições de estado;
* cancelamento;
* conclusão;
* remarcação;
* jornada de trabalho dos barbeiros.

---

## 🔌 API

A API utiliza HTTP, JSON e REST.

Principais grupos de endpoints:

### Autenticação

```text
POST /api/auth/register
POST /api/auth/login
```

### Usuários

```text
GET    /api/users/me
PUT    /api/users/me
PUT    /api/users/me/password
DELETE /api/users/me
GET    /api/users/clients
```

### Serviços

```text
GET    /api/services
PUT    /api/services/{id}
DELETE /api/services/{id}
```

### Barbeiros

```text
GET /api/barbers
```

### Agendamentos

```text
GET    /api/appointments/availability
GET    /api/appointments/me
GET    /api/appointments
POST   /api/appointments
PUT    /api/appointments/{id}/reschedule
PATCH  /api/appointments/{id}/cancel
PATCH  /api/appointments/{id}/complete
DELETE /api/appointments/{id}
```

A autorização de cada operação depende do usuário autenticado e de sua função.

---

## 🔐 Autenticação e autorização

A API utiliza **JWT Bearer Authentication**.

O login retorna:

* token JWT;
* data de expiração;
* informações básicas do usuário.

O token é utilizado pelo frontend nas requisições autenticadas.

O sistema possui funções para diferentes perfis de usuário, incluindo:

* cliente;
* administrador.

As regras de autorização são aplicadas nos endpoints que exigem acesso autenticado ou administrativo.

---

## 🗄️ Banco de dados

O backend utiliza:

* Entity Framework Core;
* SQLite.

A conexão padrão é configurada em:

```text
BackAndre.Api/appsettings.json
```

Configuração atual:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=barbearia.db"
  }
}
```

O banco SQLite é criado e atualizado utilizando as migrations do Entity Framework Core.

---

## 🌱 Dados de demonstração

Durante a inicialização da aplicação, o `DbInitializer` verifica se existem dados e cria registros de demonstração quando necessário.

São incluídos dados para:

* usuário administrador;
* usuário cliente;
* serviços;
* barbeiros;
* agendamentos de demonstração.

As credenciais de demonstração devem ser utilizadas apenas no ambiente acadêmico/local.

---

## 🧪 Testes

Para executar os testes:

```powershell
dotnet test .\BackAndre.Tests\BackAndre.Tests.csproj
```

Para executar o build completo:

```powershell
dotnet build .\BackAndre.Api\BackAndre.Api.csproj
```

Para restaurar as dependências:

```powershell
dotnet restore .\BackAndre.Api\BackAndre.Api.csproj
```

---

## ▶️ Executando a API

A partir da pasta `backend`:

```powershell
dotnet run --project .\BackAndre.Api\BackAndre.Api.csproj
```

Em desenvolvimento, a API é disponibilizada em:

```text
http://localhost:5071
```

Swagger:

```text
http://localhost:5071/swagger
```

O Swagger pode ser utilizado para:

* consultar os endpoints;
* testar requisições;
* autenticar utilizando JWT;
* verificar respostas da API.

---

## 🔄 Integração com o Frontend

O frontend localizado em:

```text
../frontend
```

utiliza esta API como fonte dos dados de domínio.

A comunicação ocorre por HTTP/JSON:

```text
Frontend
    │
    │ HTTP / JSON
    ▼
BackAndre.Api
    │
    ▼
Application
    │
    ▼
Infrastructure
    │
    ▼
SQLite
```

O frontend utiliza:

```env
VITE_API_URL=http://localhost:5071/api
```

durante o desenvolvimento local.

---

## 🌐 CORS

As origens utilizadas durante o desenvolvimento são configuradas no `appsettings.json`.

Atualmente:

```text
http://localhost:3000
http://localhost:5173
http://localhost:8080
```

Caso a aplicação frontend seja executada em outra origem, a configuração deverá ser atualizada.

---

## 🗃️ Entity Framework Core

As migrations estão localizadas em:

```text
BackAndre.Infrastructure/Migrations/
```

Como o `AppDbContext` e as migrations pertencem ao projeto `BackAndre.Infrastructure`, comandos do Entity Framework devem indicar tanto o projeto de infraestrutura quanto o projeto de inicialização da API.

Exemplo:

```powershell
dotnet ef migrations list `
  --project .\BackAndre.Infrastructure\BackAndre.Infrastructure.csproj `
  --startup-project .\BackAndre.Api\BackAndre.Api.csproj
```

Para criar uma nova migration:

```powershell
dotnet ef migrations add NomeDaMigration `
  --project .\BackAndre.Infrastructure\BackAndre.Infrastructure.csproj `
  --startup-project .\BackAndre.Api\BackAndre.Api.csproj
```

---

## 🔒 Configuração JWT

A configuração atual contém uma chave destinada ao ambiente acadêmico/local.

Exemplo:

```json
"Jwt": {
  "Key": "CHAVE-DEMONSTRACAO-NEW-AGE-TROCAR-EM-PRODUCAO-2026",
  "Issuer": "BackAndre",
  "Audience": "FrontAndre",
  "ExpirationMinutes": 120
}
```

A chave presente no repositório é uma **chave de demonstração** e não deve ser utilizada em um ambiente de produção.

Em um cenário real, a chave deverá ser fornecida por configuração segura, variável de ambiente, Secret Manager ou mecanismo equivalente.

---

## 📚 Documentação da arquitetura

A documentação detalhada da organização das camadas está disponível em:

```text
backend/ARQUITETURA.md
```

Ela descreve:

* responsabilidades das camadas;
* fluxo entre os componentes;
* organização dos projetos;
* decisões relacionadas à arquitetura.

---

## 🎓 Projeto Acadêmico

Este backend faz parte do projeto acadêmico **Projeto-Barbearia**, desenvolvido para a disciplina de **Design e Programação Orientados a Objetos** do curso de **Ciência da Computação — UNIFESO**.

A arquitetura e as tecnologias foram definidas considerando os objetivos acadêmicos da disciplina e os requisitos estabelecidos pelo grupo.

---

