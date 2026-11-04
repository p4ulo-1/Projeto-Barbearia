# Servia — Plataforma SaaS de Gestão e Atendimento para Negócios de Serviços

<p align="center">
  <strong>Gestão, agendamento, presença digital e automação em uma única plataforma</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Em%20Desenvolvimento-yellow?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Modelo-SaaS%20Multi--tenant-blue?style=for-the-badge" />
  <img src="https://img.shields.io/badge/React-TypeScript-61DAFB?style=for-the-badge&logo=react&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-.NET%2010-512BD4?style=for-the-badge&logo=.net&logoColor=white" />
</p>

---

## 🎓 Identificação Acadêmica

| Informação | Dados |
| --- | --- |
| **Instituição** | UNIFESO |
| **Curso** | Ciência da Computação |
| **Disciplina** | Design e Programação Orientados a Objetos |
| **Professor** | André Campos |
| **Projeto** | Servia (origem: New Age Barbearia) |

### Integrantes

| Integrante | Matrícula |
| --- | --- |
| Paulo Vitor Mendes Perez | 06014680 |
| João Pedro Rocha Andrade | 06014599 |
| Rikelv Ferraz da Rocha | 06015108 |
| Gabriel Guerra | 06021409 |
| Guilherme Henrique Quintanilha | 06013890 |
| Vitor Alexandre Rocha de Souza | 06014670 |


---

## 📌 Sobre o Projeto

Plataforma SaaS para negócios que trabalham com serviços agendados: barbearias, salões de beleza, nail designers, clínicas, estúdios de estética, sobrancelhas e cílios, tatuagem, massoterapia, maquiagem, depilação, tranças e outros prestadores.

Cada estabelecimento opera em seu próprio ambiente, com site individual, agendamento online, gestão de clientes e profissionais, controle financeiro e automação de atendimento — tudo sobre uma infraestrutura compartilhada.

O produto nasceu como projeto acadêmico de uma barbearia e evoluiu para uma plataforma multi-segmento. A documentação de produto, funcionalidades, modelo de classes e decisões de arquitetura está em [`docs/`](docs/).

### Proposta de valor

> Tudo o que seu negócio precisa para atender, organizar e crescer em uma única plataforma.

A solução elimina a fragmentação entre agendas, planilhas, conversas no WhatsApp, ferramentas financeiras e sistemas independentes.

---

## 🎯 Escopo do Produto

### Funcionalidades universais

Base comum disponível para todos os estabelecimentos:

- cadastro e gestão de clientes com histórico e preferências;
- catálogo de serviços, categorias, variações e combos;
- gestão de profissionais, equipes, jornadas e bloqueios;
- agendamento online com controle de disponibilidade;
- site individual com presença digital;
- comunicação e notificações;
- registro de pagamentos e gestão financeira;
- relatórios e indicadores.

### Módulos opcionais

Habilitados conforme o plano e o modelo de negócio:

- clubes de assinatura e pacotes de serviços;
- cobranças recorrentes;
- comissões e remuneração de profissionais;
- automação de atendimento pelo WhatsApp;
- fidelização e recuperação de clientes inativos;
- geração e personalização de site por inteligência artificial.

### Adaptações por segmento

Configurações que ajustam o comportamento da plataforma sem criar aplicações independentes: durações específicas, sessões prolongadas, sinal para reserva, histórico de procedimentos, pacotes de manutenção e regras de utilização de benefícios.

---

## 🏗️ Arquitetura

O repositório separa a aplicação cliente da aplicação servidora, sobre uma arquitetura em camadas.

```text
┌──────────────────────┐
│      Frontend        │
│ React + TypeScript   │
└──────────┬───────────┘
           │ HTTP / JSON
           ▼
┌──────────────────────┐
│      Servia.Api      │
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
│      Database        │
└──────────────────────┘
```

### Responsabilidades por camada

| Camada | Responsabilidade |
| --- | --- |
| **Servia.Api** | Entrada HTTP, controllers, middleware, autenticação e resolução de tenant |
| **Servia.Application** | Casos de uso, serviços, DTOs e orquestração de integrações |
| **Servia.Domain** | Entidades, agregados, objetos de valor e regras de negócio |
| **Servia.Infrastructure** | Persistência, repositórios, migrations, segurança e integrações externas |
| **Servia.Tests** | Testes automatizados de domínio e de casos de uso |

> **Nota:** os projetos no código-fonte ainda usam o prefixo `BackAndre.*` (herdado da fase acadêmica). A renomeação física das pastas e namespaces está planejada e será executada em uma etapa própria. Os comandos `dotnet` abaixo apontam para os caminhos atuais.

### Isolamento multi-tenant

Toda entidade de negócio carrega `TenantId`, e o `AppDbContext` aplica filtro global por tenant. A estratégia está detalhada em [`docs/gerenciamento/ARCH.md`](docs/gerenciamento/ARCH.md) e registrada em [`docs/gerenciamento/ADR.md`](docs/gerenciamento/ADR.md).

---

## 🎨 Frontend

Interface da aplicação, navegação, interação com o usuário e comunicação com a API.

### Tecnologias

- React
- TypeScript
- Vite
- TanStack Router
- TanStack Start
- Tailwind CSS
- Radix UI
- React Hook Form
- Zod

### Superfícies de produto

| Superfície | Público |
| --- | --- |
| Site público do estabelecimento | Clientes finais |
| Agendamento online | Clientes finais |
| Painel do estabelecimento | Equipe do tenant |
| Painel da plataforma | Administradores da plataforma |

A comunicação com o backend é realizada por HTTP utilizando a API REST.

O endereço da API pode ser configurado por meio da variável:

```env
VITE_API_URL=http://localhost:5071/api
```

Na ausência dessa variável, o frontend utiliza o endereço local da API como fallback de desenvolvimento.

Mais informações estão disponíveis em [`frontend/README.md`](frontend/README.md).

---

## ⚙️ Backend

API REST desenvolvida em **C# com ASP.NET Core**, utilizando arquitetura em camadas.

### Tecnologias

- C#
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL (produção) / SQLite (desenvolvimento e testes)
- JWT Bearer Authentication
- ASP.NET Core PasswordHasher
- Swagger / OpenAPI
- xUnit

Mais informações estão disponíveis em [`backend/README.md`](backend/README.md) e [`backend/ARQUITETURA.md`](backend/ARQUITETURA.md).

---

## 🔐 Autenticação e Autorização

A autenticação utiliza **JWT Bearer**.

O backend fornece endpoints para:

- registro;
- login;
- consulta do usuário autenticado;
- atualização dos dados do usuário;
- alteração de senha;
- exclusão da conta.

As permissões são controladas por papel dentro do tenant: proprietário, administrador, profissional, recepcionista e cliente. A verificação é sempre feita no servidor.

---

## 🗄️ Persistência

O Entity Framework Core é utilizado para:

- mapeamento das entidades;
- acesso ao banco;
- migrations;
- criação e atualização da estrutura do banco.

O backend possui um processo de inicialização que cria dados de demonstração em ambiente de desenvolvimento.

---

## 🧪 Testes

O projeto possui testes automatizados utilizando xUnit.

Os testes abrangem regras do domínio, incluindo:

- agendamentos;
- estados dos agendamentos;
- cancelamento;
- conclusão;
- remarcação;
- jornada de trabalho dos profissionais.

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

## 📚 Documentação

| Documento | Conteúdo |
| --- | --- |
| [`docs/descricao_produto.md`](docs/descricao_produto.md) | Visão de produto, segmentos e proposta de valor |
| [`docs/levantamento_lacunas.md`](docs/levantamento_lacunas.md) | Inventário do código legado e o que muda na migração (F1.4) |
| [`docs/funcionalidades.md`](docs/funcionalidades.md) | Catálogo de funcionalidades por categoria |
| [`docs/classes.md`](docs/classes.md) | Modelo de classes e diagrama Mermaid |
| [`docs/gerenciamento/Roadmap.md`](docs/gerenciamento/Roadmap.md) | Fases de entrega e critérios de saída |
| [`docs/gerenciamento/ADR.md`](docs/gerenciamento/ADR.md) | Decisões de arquitetura registradas |
| [`docs/gerenciamento/ARCH.md`](docs/gerenciamento/ARCH.md) | Arquitetura de solução |

---

## 📊 Estado Atual

**Status:** 🚧 Em desenvolvimento — sistema single-shop funcional, em transição para plataforma multi-tenant.

### Concluído

- [x] Estrutura do repositório
- [x] Interface do frontend
- [x] Modelagem do domínio
- [x] Regras de negócio principais
- [x] API ASP.NET Core
- [x] Arquitetura em camadas
- [x] Persistência com Entity Framework Core
- [x] Migrations
- [x] Autenticação JWT
- [x] Autorização por função
- [x] CRUD e operações de domínio necessárias
- [x] Integração frontend + backend
- [x] Seed de dados de demonstração
- [x] Tratamento de exceções
- [x] Testes automatizados
- [x] Documentação de produto, funcionalidades, classes e arquitetura

### Próximas etapas

Decisões estruturais confirmadas em 2026-11-04. Escopo do MVP: completo (F0–F9).

- [ ] Fase F0 — fundação técnica (Docker, CI, PostgreSQL)
- [ ] Fase F1 — multi-tenancy e migração do sistema atual
- [ ] Fase F2 — núcleo universal
- [ ] Fase F3 — site por tenant
- [ ] Fase F4 — billing da plataforma (Mercado Pago)
- [ ] Fase F5 — financeiro do tenant
- [ ] Fase F6 — comunicação e WhatsApp
- [ ] Fase F7 — módulos opcionais
- [ ] Fase F8 — IA de site
- [ ] Fase F9 — escala e operação

O detalhamento está em [`docs/gerenciamento/Roadmap.md`](docs/gerenciamento/Roadmap.md).

---

## 🌿 Versionamento

O projeto utiliza **Git e GitHub** para controle de versão e desenvolvimento colaborativo.

A branch `main` representa a versão integrada do projeto.

As alterações devem ser organizadas em commits coerentes, permitindo acompanhar a evolução do sistema e as principais decisões técnicas.

---

## 🎓 Nota sobre a Origem

A identificação acadêmica e a equipe original estão registradas no topo deste documento, na seção [Identificação Acadêmica](#-identificação-acadêmica). O código e as decisões de modelagem da fase acadêmica foram preservados e serviram de base para a evolução para plataforma SaaS.
