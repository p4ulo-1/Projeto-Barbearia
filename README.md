# Servia — Plataforma SaaS de Gestão e Atendimento para Negócios de Serviços

<p align="center">
  <strong>Gestão, agendamento, presença digital e automação em uma única plataforma</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-F0--F21%20entregues%20%2B%20AV2%20em%20andamento-yellow?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Testes-1145%20backend%20%2B%2062%20frontend-brightgreen?style=for-the-badge" />
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

## ✅ Funcionalidades Implementadas

O backend cobre o domínio completo das fases F0–F21. O frontend novo (`frontend/`) está em integração incremental com a API, no escopo da AV2.

### Implementado e coberto por testes

| Área | Estado |
| --- | --- |
| Multi-tenancy e isolamento por `TenantId` | ✅ Filtro global, FKs compostas `(TenantId, Id)` e testes cross-tenant |
| Autenticação JWT e autorização por permissão | ✅ Login, refresh com rotação, logout, papéis por tenant |
| Agenda interna | ✅ Criação, remarcação, cancelamento e conclusão; duração obtida do serviço no servidor |
| Disponibilidade | ✅ Jornada do profissional, bloqueios, expediente do estabelecimento e conflito de horário |
| Catálogo de serviços | ✅ CRUD, categorias, duração e preço |
| Gestão de profissionais | ✅ Cadastro, jornadas, bloqueios e vínculo profissional–serviço |
| Elegibilidade e limites de plano | ✅ Regra única de vaga (ativo + serviço ativo + jornada) com verificação transacional |
| Clientes | ✅ Cadastro, consulta e histórico |
| Site público por tenant | ✅ Resolução por subdomínio/`Host`, seções publicadas e `BookingWidget` |
| Agendamento público | ✅ Reserva sem sessão, com as mesmas regras da agenda interna |
| Financeiro do tenant | ✅ Registro de pagamentos e visão mínima |
| Billing da plataforma | ✅ Planos, assinaturas, faturas, franquia de IA e política de inadimplência |
| Mercado Pago — checkout recorrente | ⚠️ Implementado no código, **não homologado em sandbox** (ver limitações) |
| Webhooks financeiros | ⚠️ Inbox durável e job de processamento implementados; validação ponta a ponta pendente |
| IA de site | ✅ Geração e alteração de site com schema fechado |
| Jobs em background | ✅ Rotinas agendadas (inbox, sincronização, notificações) |

### Principais limitações atuais

- **Mercado Pago não homologado.** O gateway implementa `POST /preapproval` e o checkout recorrente, mas a contratação real depende de credenciais e validação em sandbox. Nenhuma cobrança é considerada pronta para produção.
- **Frontend em integração.** O `frontend/` cobre agenda, clientes, serviços, equipe, financeiro, assinatura e site. Onboarding, editor de site e contratação ainda têm dependências da AV2.
- **WhatsApp pausado.** A integração com a WhatsApp Business Platform está suspensa (ADR-023).
- **Segurança de sessão em avaliação.** Tokens ficam em `sessionStorage`; cookie `HttpOnly`, CSRF e CORS definitivos ainda não foram fechados.
- **Observabilidade mínima.** Apenas health checks; sem métricas, trace ou alertas externos.

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
| **Servia.Api** | Entrada HTTP, controllers, middleware, autenticação, resolução de tenant e jobs agendados |
| **Servia.Application** | Casos de uso, serviços, DTOs e orquestração de integrações |
| **Servia.Domain** | Entidades, agregados, objetos de valor e regras de negócio |
| **Servia.Infrastructure** | Persistência, repositórios, migrations, segurança e integrações externas |
| **Servia.Tests** | Testes automatizados de domínio, persistência, API e integração |

### Organização do repositório

```text
src/
  Servia.Api/            API REST, controllers, jobs
  Servia.Application/    casos de uso e serviços
  Servia.Domain/         entidades e regras de negócio
  Servia.Infrastructure/ EF Core, repositórios, migrations, integrações
  Servia.Tests/          suíte de testes (xUnit + Testcontainers)
  Servia.slnx            solução
frontend/                frontend atual (TanStack Start + React)
web/                     frontend anterior, preservado como referência
docs/                    documentação de produto, arquitetura, operação e jurídico
tests/load/              teste de carga com k6
scripts/                 utilitários operacionais
docker-compose.yml       ambiente completo (API + PostgreSQL)
build.cmd                wrapper de build para Windows
```

### Isolamento multi-tenant

Toda entidade de negócio carrega `TenantId`, e o `AppDbContext` aplica filtro global por tenant. A estratégia está detalhada em [`docs/arquitetura/ARCH.md`](docs/arquitetura/ARCH.md) e registrada em [`docs/arquitetura/ADR.md`](docs/arquitetura/ADR.md).

---

## 🎨 Frontend

Interface da aplicação, navegação, interação com o usuário e comunicação com a API.

### Tecnologias

- React 19
- TypeScript (modo estrito)
- Vite
- TanStack Start e TanStack Router
- TanStack Query
- Tailwind CSS 4 e componentes Radix UI
- Vitest e Testing Library
- Playwright (homologação em navegador real)

### Superfícies de produto

| Superfície | Público | Estado |
| --- | --- | --- |
| Site público do estabelecimento | Clientes finais | ✅ |
| Agendamento online (widget no site publicado) | Clientes finais | ✅ |
| Painel do estabelecimento | Equipe do tenant | ✅ |
| Painel da plataforma | Administradores da plataforma | ✅ |

O frontend adotado para continuar o desenvolvimento está em [`frontend/`](frontend/). A implementação anterior em [`web/`](web/) permanece preservada até a substituição operacional. A comunicação com a API é centralizada em `frontend/src/lib/api/http.ts`.

O endereço da API é configurado por variável de ambiente:

```env
VITE_API_URL=/
```

Em desenvolvimento, `/api` é encaminhado pelo Vite para `http://localhost:5222` por padrão; `SERVIA_API_PROXY_TARGET` troca esse alvo, por exemplo para `http://localhost:8080` do Docker Compose. Sem `VITE_API_URL`, o frontend usa a mesma origem e **não** ativa dados simulados. A demonstração isolada exige `VITE_DEMO_MODE=true`.

**Sessão no frontend atual:** tokens reais ficam em `sessionStorage` por aba; o refresh token é rotacionado pela API. A política de segurança final permanece sob avaliação no escopo apropriado.

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

**1145 atributos `[Fact]`/`[Theory]` no backend**, executados contra **PostgreSQL 17** via Testcontainers, e **62 testes de frontend** com Vitest.

| Grupo | Cobertura |
| --- | --- |
| Domínio | Agendamentos, catálogo, equipe, clientes, billing, financeiro, mensageria, módulos, IA, operações |
| Persistência | Migrations, schema, índices, seed, FKs compostas |
| Isolamento multi-tenant | Leitura, criação, alteração, exclusão, header forjado, assinaturas |
| Concorrência | Requisições simultâneas, sobreposição de horários, `ScheduleLock`, ocupação de vagas do plano |
| API | Autenticação, autorização, paginação, provisionamento, endpoints públicos |
| Integrações | Mercado Pago, WhatsApp, OpenAI (com dublês) |

### Backend

```powershell
cd src
..\build.cmd test Servia.slnx
```

> O `build.cmd` define as variáveis de ambiente do Windows que o NuGet exige neste ambiente.

**Requisito:** Docker em execução — os testes sobem containers PostgreSQL 17 isolados.

### Frontend

```powershell
cd frontend
npm test
npm run typecheck
npm run build
```

### Homologação em navegador real

O `frontend/` inclui scripts de homologação da Fase 1 da AV2, com API real, PostgreSQL isolado e Chromium via Playwright:

```powershell
cd frontend
npm run homolog:run
```

Os artefatos (relatório e traces) são gravados em `frontend/.homolog-artifacts/`, ignorado pelo Git. Instruções completas em [`frontend/README.md`](frontend/README.md).

---

## ▶️ Executando o projeto

### Requisitos

- .NET 10 SDK
- Node.js 22 e npm
- Docker (para PostgreSQL e para os testes)

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
..\build.cmd run --project Servia.Api\Servia.Api.csproj --urls http://localhost:5222
```

Requer `ConnectionStrings__DefaultConnection` apontando para um PostgreSQL acessível.

### Frontend

Em outro terminal:

```powershell
cd frontend
npm ci
npm run dev
```

O Vite informa a porta local ao iniciar. Para usar a API do Docker Compose, defina o destino do proxy antes de `npm run dev`:

```powershell
$env:SERVIA_API_PROXY_TARGET="http://localhost:8080"; npm run dev
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
| [`docs/juridico/POLITICA_DE_PRIVACIDADE.md`](docs/juridico/POLITICA_DE_PRIVACIDADE.md) | Política de privacidade e tratamento de dados |
| [`docs/juridico/TERMOS_DE_USO.md`](docs/juridico/TERMOS_DE_USO.md) | Termos de uso da plataforma |
| [`frontend/README.md`](frontend/README.md) | Desenvolvimento, variáveis de ambiente e verificações do frontend |
| [`tests/load/README.md`](tests/load/README.md) | Teste de carga com k6 |

> **Estrutura:** `docs/produto/` (visão e catálogo), `docs/arquitetura/` (arquitetura, modelo e decisões), `docs/gerenciamento/` (roadmap), `docs/operacao/` (operação), `docs/juridico/` (LGPD e termos).

---

## 📊 Estado do Desenvolvimento

**Status:** ✅ Fases F0–F21 entregues no backend. 🔄 AV2 em andamento no frontend e na integração de billing.

**1145 atributos `[Fact]`/`[Theory]`** no backend e **62 testes** no frontend (verificado em 2026-11-19). Operação comercial plena pendente — ver §"O que falta para operação comercial".

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

### AV2 — Fase 1 (agenda)

| Requisito | Estado |
| --- | --- |
| AV2-01 — Criação centralizada na agenda interna | ✅ Homologado em navegador real |
| AV2-02 — Remarcação consistente | ✅ Homologado em navegador real |
| AV2-03 — Contrato único público/interno | ✅ Homologado para o núcleo da agenda |
| AV2-04 — Fluxo completo de agenda | ✅ Homologado para a Fase 1 |

### AV2 — Fase 2 (billing e Mercado Pago)

| Requisito | Estado |
| --- | --- |
| AV2-09 — Contratação e alteração de planos | ⚠️ Parcial |
| AV2-19 — Contratação recorrente no Mercado Pago | ⚠️ Parcial — código implementado, sem homologação em sandbox |

O ciclo financeiro de assinaturas (faturas, inadimplência, webhooks com inbox durável) foi implementado no backend. A contratação real depende de credenciais e validação externa.

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

### O que falta para operação comercial

As fases F0–F21 entregaram domínio, API, integrações e frontend. O que resta **não é fase de roadmap** — são pendências identificadas na auditoria do ARCH:

| # | Pendência | Impacto |
| --- | --- | --- |
| 1 | **Homologação do Mercado Pago em sandbox** — checkout recorrente e webhooks | Contratação implementada, cobrança real não validada |
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
