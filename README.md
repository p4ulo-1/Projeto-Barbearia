# 💈 New Age Barbearia

<p align="center">
  <strong>Projeto Integrador — Design e Programação Orientados a Objetos</strong>
</p>

<p align="center">
  Aplicação web para gerenciamento de uma barbearia, desenvolvida como projeto acadêmico colaborativo.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Em%20Desenvolvimento-yellow?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Projeto-Acad%C3%AAmico-blue?style=for-the-badge" />
  <img src="https://img.shields.io/badge/React-TypeScript-61DAFB?style=for-the-badge&logo=react&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-.NET%2010-512BD4?style=for-the-badge&logo=.net&logoColor=white" />
</p>

---

## 🎓 Identificação

| Informação      | Dados                                     |
| --------------- | ----------------------------------------- |
| **Instituição** | UNIFESO                                   |
| **Curso**       | Ciência da Computação                     |
| **Disciplina**  | Design e Programação Orientados a Objetos |
| **Professor**   | André Campos                              |
| **Projeto**     | New Age Barbearia                         |

### Integrantes

| Integrante                     | Matrícula |
| ------------------------------ | --------- |
| Paulo Vitor Mendes Perez       | 06014680  |
| João Pedro Rocha Andrade       | 06014599  |
| Rikelv Ferraz da Rocha         | 06015108  |
| Gabriel Guerra                 | 06021409  |
| Guilherme Henrique Quintanilha | 06013890  |
| Vitor Alexandre Rocha de Souza | 06014670  |

---

## 📌 Sobre o Projeto

A New Age Barbearia é uma aplicação web desenvolvida como projeto integrador da disciplina de **Design e Programação Orientados a Objetos**.

O sistema representa processos de uma barbearia por meio de uma aplicação envolvendo usuários, serviços, profissionais, horários e agendamentos.

O projeto busca aplicar conceitos de orientação a objetos e desenvolvimento de sistemas, incluindo:

* identidade, estado e comportamento;
* encapsulamento;
* responsabilidades e colaboração entre objetos;
* regras de negócio;
* transições de estado;
* persistência de dados;
* autenticação e autorização;
* testes automatizados;
* integração entre frontend e backend.

Atualmente, o sistema possui **frontend integrado a uma API ASP.NET Core**, com persistência em SQLite.

---

## 💈 Funcionalidades

A aplicação contempla os principais processos do sistema de gerenciamento da barbearia:

* cadastro de clientes;
* autenticação de clientes e administradores;
* gerenciamento de usuários;
* gerenciamento de serviços;
* consulta de barbeiros;
* consulta de disponibilidade;
* criação de agendamentos;
* consulta dos próprios agendamentos;
* remarcação de agendamentos;
* cancelamento de agendamentos;
* conclusão de atendimentos;
* controle de estados dos agendamentos;
* gerenciamento administrativo de clientes;
* gerenciamento administrativo de serviços;
* autenticação baseada em JWT;
* autorização por função.

---

## 🏗️ Estrutura do Projeto

O repositório separa a aplicação cliente da aplicação servidora:

```text
Projeto-Barbearia/
│
├── frontend/
│   ├── src/
│   ├── public/
│   ├── package.json
│   ├── bunfig.toml
│   └── ...
│
├── backend/
│   ├── BackAndre.Api/
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── BackAndre.Api.csproj
│   │
│   ├── BackAndre.Application/
│   │   ├── DTOs/
│   │   ├── Services/
│   │   └── BackAndre.Application.csproj
│   │
│   ├── BackAndre.Domain/
│   │   ├── Models/
│   │   └── BackAndre.Domain.csproj
│   │
│   ├── BackAndre.Infrastructure/
│   │   ├── Data/
│   │   ├── Repositories/
│   │   ├── Migrations/
│   │   ├── Security/
│   │   └── BackAndre.Infrastructure.csproj
│   │
│   ├── BackAndre.Tests/
│   │   └── ...
│   │
│   ├── ARQUITETURA.md
│   └── ...
│
└── README.md
```

---

## 🎨 Frontend

O frontend é responsável pela interface da aplicação, navegação, interação com o usuário e comunicação com a API.

### Tecnologias

* React
* TypeScript
* Vite
* TanStack Router
* TanStack Start
* Tailwind CSS
* Radix UI
* React Hook Form
* Zod

A comunicação com o backend é realizada por HTTP utilizando a API REST.

O endereço da API pode ser configurado por meio da variável:

```env
VITE_API_URL=http://localhost:5071/api
```

Na ausência dessa variável, o frontend utiliza o endereço local da API como fallback de desenvolvimento.

Mais informações estão disponíveis em [`frontend/README.md`](frontend/README.md).

---

## ⚙️ Backend

O backend é uma API REST desenvolvida em **C# com ASP.NET Core**, utilizando uma arquitetura em camadas.

A organização atual é:

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

As responsabilidades são distribuídas entre:

* **BackAndre.Api** — entrada HTTP, controllers, middleware e configuração;
* **BackAndre.Application** — casos de uso, serviços e DTOs;
* **BackAndre.Domain** — entidades e regras de domínio;
* **BackAndre.Infrastructure** — persistência, repositories, migrations e segurança;
* **BackAndre.Tests** — testes automatizados.

O backend utiliza:

* C#
* .NET 10
* ASP.NET Core Web API
* Entity Framework Core
* SQLite
* JWT Bearer Authentication
* ASP.NET Core PasswordHasher
* Swagger / OpenAPI
* xUnit

Mais informações estão disponíveis em [`backend/README.md`](backend/README.md).

---

## 🔄 Integração

O frontend e o backend funcionam como partes integradas da mesma aplicação.

```text
┌──────────────────────┐
│      Frontend        │
│ React + TypeScript   │
└──────────┬───────────┘
           │ HTTP / JSON
           ▼
┌──────────────────────┐
│      BackAndre.Api   │
│    ASP.NET Core      │
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│   Application        │
│      Services        │
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│   Infrastructure     │
│ Repositories / EF    │
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│       SQLite         │
└──────────────────────┘
```

A API fornece os dados reais utilizados pela interface.

O navegador mantém apenas o token JWT necessário para a sessão. Os dados de domínio são persistidos no backend.

---

## 🔐 Autenticação

A autenticação utiliza **JWT Bearer**.

O backend fornece endpoints para:

* registro;
* login;
* consulta do usuário autenticado;
* atualização dos dados do usuário;
* alteração de senha;
* exclusão da conta.

As permissões são controladas de acordo com a função do usuário, incluindo clientes e administradores.

---

## 🗄️ Persistência

Os dados são armazenados em um banco SQLite.

O Entity Framework Core é utilizado para:

* mapeamento das entidades;
* acesso ao banco;
* migrations;
* criação e atualização da estrutura do banco.

O backend também possui um processo de inicialização que cria dados de demonstração quando necessário.

---

## 🧪 Testes

O projeto possui testes automatizados utilizando xUnit.

Os testes abrangem principalmente regras do domínio, incluindo comportamentos relacionados a:

* agendamentos;
* estados dos agendamentos;
* cancelamento;
* conclusão;
* remarcação;
* jornada de trabalho dos barbeiros.

Para executar os testes:

```powershell
dotnet test .\backend\BackAndre.Tests\BackAndre.Tests.csproj
```

---

## ▶️ Executando o projeto

### Backend

A partir da raiz do repositório:

```powershell
dotnet restore .\backend\BackAndre.Api\BackAndre.Api.csproj
dotnet build .\backend\BackAndre.Api\BackAndre.Api.csproj
dotnet run --project .\backend\BackAndre.Api\BackAndre.Api.csproj
```

Durante o desenvolvimento, a API fica disponível em:

```text
http://localhost:5071
```

O Swagger pode ser acessado em:

```text
http://localhost:5071/swagger
```

### Frontend

Em outro terminal:

```powershell
cd frontend
bun install
bun run dev
```

O endereço apresentado pelo Vite deve ser utilizado para acessar a aplicação.

Para que a integração funcione corretamente, o backend deve estar em execução.

---

## 📊 Estado Atual

**Status:** 🚧 Em desenvolvimento — integração frontend/backend funcional

### Concluído

* [x] Estrutura do repositório
* [x] Interface do frontend
* [x] Modelagem do domínio
* [x] Regras de negócio principais
* [x] API ASP.NET Core
* [x] Arquitetura em camadas
* [x] Persistência com Entity Framework Core e SQLite
* [x] Migrations
* [x] Autenticação JWT
* [x] Autorização por função
* [x] CRUD e operações de domínio necessárias
* [x] Integração frontend + backend
* [x] Seed de dados de demonstração
* [x] Tratamento de exceções
* [x] Testes automatizados
* [x] Validação da API pelo Swagger
* [x] Validação dos principais fluxos pelo frontend

### Próximas etapas

* [ ] Refinamento da documentação acadêmica
* [ ] Evolução do modelo de classes conforme os requisitos da disciplina
* [ ] Registro das decisões técnicas e evidências
* [ ] Preparação da apresentação e defesa do projeto
* [ ] Melhorias futuras conforme os requisitos definidos pelo grupo e pelo professor

---

## 🌿 Versionamento

O projeto utiliza **Git e GitHub** para controle de versão e desenvolvimento colaborativo.

A branch `main` representa a versão integrada do projeto.

As alterações devem ser organizadas em commits coerentes, permitindo acompanhar a evolução do sistema e as principais decisões técnicas.

---

## 🎓 Projeto Acadêmico

Este repositório foi desenvolvido exclusivamente para fins acadêmicos como parte das atividades da disciplina de **Design e Programação Orientados a Objetos** do curso de **Ciência da Computação** do **UNIFESO**.

O sistema continuará evoluindo de acordo com os requisitos da disciplina e as decisões do grupo.

