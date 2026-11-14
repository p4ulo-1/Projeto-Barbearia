# Servia — Plataforma SaaS de Gestão e Atendimento para Negócios de Serviços

<p align="center">
  <strong>Gestão, agendamento, presença digital e automação em uma única plataforma</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-F0--F21%20entregues-brightgreen?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Testes-1011%20automatizados-brightgreen?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Modelo-SaaS%20Multi--tenant-blue?style=for-the-badge" />
  <img src="https://img.shields.io/badge/PostgreSQL-17-336791?style=for-the-badge&logo=postgresql&logoColor=white" />
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

## Integrantes

| Integrante | Matrícula |
| --- | --- |
| Paulo Vitor Mendes Perez | 06014680 |
| João Pedro Rocha Andrade | 06014599 |
| Rikelv Ferraz da Rocha | 06015108 |
| Gabriel Pinheiro da Silva Guerra | 06021409 |
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

### Módulos disponíveis em todos os planos

**Todos os planos liberam todos os módulos.** A diferenciação entre planos está na quantidade de profissionais incluídos, no grau de personalização do site e na franquia mensal de IA — nunca no acesso a módulos. Os módulos são:

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
| **Servia.Tests** | Testes automatizados de domínio, persistência, API e integração |

### Isolamento multi-tenant

Toda entidade de negócio carrega `TenantId`, e o `AppDbContext` aplica filtro global por tenant. A estratégia está detalhada em [`docs/arquitetura/ARCH.md`](docs/arquitetura/ARCH.md) e registrada em [`docs/arquitetura/ADR.md`](docs/arquitetura/ADR.md).

---

## 🎨 Frontend

Interface da aplicação, navegação, interação com o usuário e comunicação com a API.

### Tecnologias

- React 19
- TypeScript (modo estrito)
- Vite
- React Router

### Superfícies de produto

| Superfície | Público | Estado |
| --- | --- | --- |
| Site público do estabelecimento | Clientes finais | ✅ |
| Agendamento online | Clientes finais | ✅ |
| Painel do estabelecimento | Equipe do tenant | ✅ |
| Painel da plataforma | Administradores da plataforma | ✅ |

O código está em [`web/`](web/). A comunicação com o backend é HTTP, centralizada em um cliente de API tipado — nenhum componente chama `fetch` diretamente.

O endereço da API é configurado por variável de ambiente:

```env
VITE_API_URL=http://localhost:8080
```

Na ausência da variável, o frontend usa `http://localhost:8080` como padrão de desenvolvimento.

**Segurança de sessão:** o token de acesso fica em memória (um XSS não o lê); o refresh token fica em `localStorage` porque precisa sobreviver ao recarregamento.

---

## ⚙️ Backend

API REST desenvolvida em **C# com ASP.NET Core**, utilizando arquitetura em camadas.

### Tecnologias

- C# / .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- **PostgreSQL 17** — único provider, em desenvolvimento, testes e produção
- JWT Bearer Authentication (PBKDF2-HMAC-SHA256, 600.000 iterações)
- Autorização por permissão
- OpenAPI (apenas em desenvolvimento)
- xUnit + Testcontainers

O código está em [`src/`](src/). A arquitetura está em [`docs/arquitetura/ARCH.md`](docs/arquitetura/ARCH.md).

---

## 🔐 Autenticação e Autorização

A autenticação utiliza **JWT Bearer**.

Endpoints disponíveis:

| Endpoint | Função |
| --- | --- |
| `POST /api/auth/sign-in` | Autentica e emite tokens |
| `POST /api/auth/refresh` | Renova a sessão (rotação de token) |
| `POST /api/auth/sign-out` | Revoga o refresh token |

**O tenant vem exclusivamente da claim `tenant_id` do token**, assinada pelo servidor. O header `X-Tenant-Id` não é aceito. Na renovação, o tenant é resolvido a partir do próprio refresh token, que é opaco.

As permissões são controladas por papel dentro do tenant: proprietário, administrador, profissional, recepcionista e cliente. A verificação é sempre feita no servidor, por `RequirePermissionAttribute`.

---

## 🗄️ Persistência

O Entity Framework Core é utilizado para:

- mapeamento das entidades;
- acesso ao banco;
- migrations versionadas;
- criação e atualização da estrutura do banco.

**Isolamento multi-tenant em três camadas:**

1. **Filtro global** — toda entidade `ITenantScoped` é restrita ao tenant corrente. Sem tenant resolvido, nada é visível.
2. **Chaves estrangeiras compostas** `(TenantId, Id)` — o banco rejeita associação entre registros de tenants diferentes.
3. **Testes cross-tenant** — a suíte prova que um estabelecimento não acessa dados de outro.

O schema é aplicado por migrations (`MigrateAsync`), nunca por `EnsureCreated`. O seed cria os quatro planos comerciais na inicialização.

---

## 🧪 Testes

**1011 atributos `[Fact]`/`[Theory]`**, executados contra **PostgreSQL 17** via Testcontainers.

| Grupo | Cobertura |
| --- | --- |
| Domínio | Agendamentos, catálogo, equipe, clientes, billing, financeiro, mensageria, módulos, IA, operações |
| Persistência | Migrations, schema, índices, seed, FKs compostas |
| Isolamento multi-tenant | Leitura, criação, alteração, exclusão, header forjado, assinaturas |
| Concorrência | Requisições simultâneas, sobreposição de horários, `ScheduleLock` |
| API | Autenticação, autorização, paginação, provisionamento |
| Integrações | Mercado Pago, WhatsApp, OpenAI (com dublês) |

Para executar:

```powershell
cd src
..\build.cmd test Servia.slnx
```

> O `build.cmd` define as variáveis de ambiente do Windows que o NuGet exige neste ambiente.

**Requisito:** Docker em execução — os testes sobem containers PostgreSQL 17 isolados.

---

## ▶️ Executando o projeto

### Ambiente completo (Docker)

```powershell
docker-compose up --build -d
```

API em `http://localhost:8080`, PostgreSQL em `localhost:5432`.

```powershell
curl.exe http://localhost:8080/health
```

### Backend direto

```powershell
cd src
..\build.cmd run --project Servia.Api\Servia.Api.csproj --urls http://localhost:5099
```

Requer `ConnectionStrings__DefaultConnection` apontando para um PostgreSQL acessível.

### Frontend

Em outro terminal:

```powershell
cd web
npm install
npm run dev
```

Aplicação em `http://localhost:5173`. Por padrão aponta para `http://localhost:8080`; para usar a API local:

```powershell
$env:VITE_API_URL="http://localhost:5099"; npm run dev
```

### Endpoints de saúde

| Endpoint | Verifica | Uso |
| --- | --- | --- |
| `/health` | API e banco | Monitoramento |
| `/health/live` | Apenas o processo | Orquestrador decide reiniciar |
| `/health/ready` | API e banco | Balanceador decide enviar tráfego |

---

## 📚 Documentação

A documentação está organizada por finalidade:

| Documento | Conteúdo |
| --- | --- |
| [`docs/produto/descricao_produto.md`](docs/produto/descricao_produto.md) | Visão, objetivos, proposta de valor, público e princípios do produto |
| [`docs/produto/funcionalidades.md`](docs/produto/funcionalidades.md) | Catálogo de funcionalidades com estado real, planos e regras comerciais |
| [`docs/arquitetura/ARCH.md`](docs/arquitetura/ARCH.md) | Arquitetura de solução: implementado, aprovado e planejado |
| [`docs/arquitetura/classes.md`](docs/arquitetura/classes.md) | Modelo de classes, regras de negócio, decisões de modelagem e diagramas |
| [`docs/arquitetura/ADR.md`](docs/arquitetura/ADR.md) | Decisões arquiteturais e comerciais (ADR-001 a ADR-027) |
| [`docs/gerenciamento/Roadmap.md`](docs/gerenciamento/Roadmap.md) | Fases de entrega, estado de execução e pendências |
| [`docs/operacao/ONBOARDING.md`](docs/operacao/ONBOARDING.md) | Colocar o primeiro cliente no ar |
| [`docs/operacao/RUNBOOK.md`](docs/operacao/RUNBOOK.md) | Procedimentos operacionais e de incidentes |
| [`tests/load/README.md`](tests/load/README.md) | Teste de carga com k6 |

> **Estrutura:** `docs/produto/` (visão e catálogo), `docs/arquitetura/` (arquitetura, modelo e decisões), `docs/gerenciamento/` (roadmap), `docs/operacao/` (operação).

---

## 📊 Estado Atual

**Status:** ✅ Fases F0–F21 entregues. **1011 atributos `[Fact]`/`[Theory]`** no código atual (verificado em 2026-11-14). Operação comercial plena pendente — ver §"O que falta para operação comercial".

### Fases concluídas

| Fase | Escopo | Entregas |
| --- | --- | --- |
| F0 | Fundação técnica (Docker, CI, PostgreSQL) | 7 |
| F1 | Multi-tenancy e isolamento | 13 |
| F2 | Núcleo universal (agenda, catálogo, equipe, clientes) | 12 |
| F3 | Site por tenant | 8 |
| F4 | Billing da plataforma | 17 |
| F5 | Financeiro do tenant | 8 |
| F6 | Comunicação e WhatsApp | 13 |
| F7 | Módulos opcionais (clubes, pacotes, fidelidade, NPS) | 7 |
| F8 | IA de site | 12 |
| F9 | Escala e operação | 7 |
| F10 | Autenticação e autorização | 8 |
| F11 | Persistência de produção (migrations) | 5 |
| F12 | Integrações (Mercado Pago, WhatsApp, OpenAI) | 7 |
| F13 | API de negócio | 11 |
| F14 | Frontend do SaaS | 10 |
| F15 | Go-live | 7 |
| F16 | Billing com escrita | 7 |
| F17 | Webhooks | 6 |
| F18 | Frontend de escrita | 10 |
| F19 | Jobs em background | 9 |
| F20 | Bootstrap e operação | 2 |
| F21 | Cadastro público e landing page | 9 |

### Auditoria de qualidade (2026-11-11)

Oito etapas executadas, cada uma com commit independente:

| Etapa | Correção |
| --- | --- |
| 1 | SQLite substituído por PostgreSQL 17 em todo o projeto |
| 2 | `Subscription` isolada por tenant |
| 3 | Tenant resolvido apenas pelo token |
| 4 | Chaves estrangeiras compostas `(TenantId, Id)` |
| 5 | Concorrência protegida por transação serializável |
| 6 | Agregações executadas no banco |
| 7 | Riscos R1–R5 corrigidos |
| 8 | Suíte de integração multi-tenant |

### Verificação

```text
Build:  0 erros
Testes: 1011 atributos [Fact]/[Theory] (PostgreSQL 17 via Testcontainers)
Docker: API e PostgreSQL healthy
```

> **Números verificados em 2026-11-14.** A suíte completa não foi reexecutada nesta atualização documental; o número refere-se aos atributos de teste presentes no código.

### O que falta para operação comercial

As fases F0–F21 entregaram domínio, API, integrações e frontend. O que resta **não é fase de roadmap** — são pendências identificadas na auditoria do ARCH:

| # | Pendência | Impacto |
| --- | --- | --- |
| 1 | **Ciclo financeiro do billing** — gateway, fatura, inadimplência | O tenant contrata e nada é cobrado |
| 2 | **Cookie `HttpOnly` + CSRF + CORS** | Segurança obrigatória; o novo frontend depende |
| 3 | **Política de consumo de IA** — compra de créditos, aquisição automática, notificação | Franquia e bloqueio existem; o resto não |
| 4 | **Cobrança de alteração pós-publicação** | Tabela aprovada, não implementada |
| 5 | **Observabilidade** — logs, métricas, trace, alertas | Só health checks |
| 6 | **Notificações** — criação, preferências, canais | Job existe; fila nunca alimentada |
| 7 | **Lock distribuído para jobs** | Rotinas duplicam em múltiplas instâncias |
| 8 | **Bloqueio de escrita por inadimplência** | `Tenant.CanWrite` não é aplicado |

O detalhamento está em [`docs/gerenciamento/Roadmap.md`](docs/gerenciamento/Roadmap.md) §"O que falta para operação comercial" e no catálogo em [`docs/produto/funcionalidades.md`](docs/produto/funcionalidades.md) §5.

### O que falta para produção

Além das fases acima, infraestrutura e credenciais:

- Domínio próprio e certificado SSL
- Agregador de logs externo
- Ambiente de produção provisionado
- Credenciais reais dos provedores (Mercado Pago, OpenAI)
- Acesso à WhatsApp Business Platform, quando a integração for retomada

> **WhatsApp (2026-11-14):** o estabelecimento é titular da própria conta WhatsApp Business Platform e paga diretamente à Meta. O Servia fornece integração, orientação e estimativas, mas **não intermedia a cobrança**. A integração está **pausada** (ADR-023).

---

## 🌿 Versionamento

O projeto utiliza **Git e GitHub** para controle de versão e desenvolvimento colaborativo.

A branch `main` representa a versão integrada do projeto.

As alterações devem ser organizadas em commits coerentes, permitindo acompanhar a evolução do sistema e as principais decisões técnicas.

---

## 🎓 Nota sobre a Origem

A identificação acadêmica e a equipe original estão registradas no topo deste documento, na seção [Identificação Acadêmica](#-identificação-acadêmica). O código acadêmico foi removido do repositório em 2026-11-10 (ADR-001); a decisão e o contexto estão em [`docs/arquitetura/ADR.md`](docs/arquitetura/ADR.md) (ADR-001).
