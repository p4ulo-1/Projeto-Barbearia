# Architecture Decision Records (ADR)

> **Status:** v1 aprovada em 2026-11-04. Todos os 14 ADRs estão aceitos.
> **Formato:** [ADR-NNN] Título — Status — Data.
> **Decisões:** confirmadas pelo responsável do produto; ver também `docs/funcionalidades.md` seção 0 e `docs/gerenciamento/ARCH.md` seção 13.

---

## Índice

| ADR | Título | Status |
| --- | --- | --- |
| ADR-001 | Reaproveitar o sistema atual como primeiro tenant | ✅ Aceito |
| ADR-002 | Multi-tenancy por banco único com `TenantId` | ✅ Aceito |
| ADR-003 | PostgreSQL como banco de produção | ✅ Aceito |
| ADR-004 | Manter arquitetura em camadas com interfaces de repositório | Aceito |
| ADR-005 | Abstrair gateways de pagamento com `IPaymentGateway` | ✅ Aceito |
| ADR-006 | WhatsApp via Cloud API oficial da Meta com abstração | ✅ Aceito |
| ADR-007 | Site gerado por IA sobre biblioteca de componentes controlada | ✅ Aceito |
| ADR-008 | Aprovação obrigatória de alterações geradas por IA | Aceito |
| ADR-009 | Separação entre `User` (identidade) e `Customer` (relacionamento) | Aceito |
| ADR-010 | Generalizar `Barber` em `Professional` | Aceito |
| ADR-011 | Docker + GitHub Actions + PaaS na primeira fase | ✅ Aceito |
| ADR-012 | LGPD: tenant como controlador, plataforma como operadora | ✅ Aceito |
| ADR-013 | Auditoria imutável por inserção | Aceito |
| ADR-014 | Objeto de valor `Money` no lugar de `decimal` puro | Aceito |

---

## ADR-001 — Reaproveitar o sistema atual como primeiro tenant

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
O projeto nasceu como aplicação acadêmica de uma barbearia, com backend .NET 10 em camadas, frontend React e regras de domínio testadas. A visão de produto exige uma plataforma SaaS multi-segmento. Existem três caminhos: reaproveitar o código, reescrever do zero ou manter dois produtos.

### Alternativas
1. **Reaproveitar como primeiro tenant** — menor esforço, preserva regras testadas, mas exige refatoração para multi-tenancy.
2. **Reescrever do zero** — modelo mais limpo, porém descarta trabalho funcional e testes.
3. **Manter dois produtos** — duplica manutenção e divergência de código.

### Decisão
Adotar a alternativa 1: o sistema atual vira o primeiro tenant da plataforma. Backend e frontend são evoluídos, não substituídos.

### Consequências
- **Positivas:** regras de agendamento já testadas são preservadas; entrega mais rápida do MVP; o primeiro tenant serve de caso real de validação.
- **Negativas:** refatoração de entidades existentes para incluir `TenantId`; risco de arrastar decisões do modelo single-shop.
- **Mitigação:** introduzir `TenantId` na fase F1 com testes de isolamento obrigatórios.

---

## ADR-002 — Multi-tenancy por banco único com `TenantId`

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
A plataforma atenderá milhares de estabelecimentos. É preciso escolher a estratégia de isolamento de dados.

### Alternativas
1. **Banco único com `TenantId` e filtro global** — menor custo operacional, migrations únicas, mas exige disciplina rigorosa de filtro.
2. **Schema por tenant** — isolamento intermediário, porém migrations multiplicadas por tenant.
3. **Banco por tenant** — isolamento máximo, com custo operacional e de backup muito alto.

### Decisão
Adotar a alternativa 1: banco único, `TenantId` em todas as entidades de negócio e filtro global no EF Core alimentado por `ITenantContext`.

### Consequências
- **Positivas:** uma migration para todos os tenants; backup e operação simplificados; custo por tenant baixo.
- **Negativas:** risco de vazamento entre tenants se o filtro for contornado; consultas cross-tenant precisam de cuidado.
- **Mitigação:** teste automatizado que falha se o filtro global for removido; revisão obrigatória de qualquer `IgnoreQueryFilters()`.

---

## ADR-003 — PostgreSQL como banco de produção

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
O sistema atual usa SQLite, adequado para desenvolvimento e demonstração acadêmica. SaaS multi-tenant exige concorrência, backup e escala.

### Alternativas
1. **PostgreSQL** — concorrência robusta, tipos ricos (`jsonb`, `timestamptz`), padrão de mercado.
2. **Manter SQLite** — zero migração, mas limita escrita concorrente e escala.
3. **SQL Server** — integração natural com .NET, porém custo de licença.

### Decisão
Adotar PostgreSQL em produção. SQLite permanece apenas para testes locais rápidos.

### Consequências
- **Positivas:** suporte a `jsonb` para configuração de seções do site e snapshots de IA; concorrência adequada.
- **Negativas:** migration de dados existentes; ajuste de índices (`NOCASE` do SQLite não existe no Postgres).
- **Mitigação:** executar a migração em ambiente isolado na fase F0 antes de tocar dados reais.

---

## ADR-004 — Manter arquitetura em camadas com interfaces de repositório

**Status:** Aceito
**Data:** 2026-11-04

### Contexto
O backend já está organizado em `Api → Application → Infrastructure → Domain`, mas os repositórios são classes concretas sem contratos, o que dificulta testes e substituição.

### Alternativas
1. **Manter camadas e adicionar interfaces** — evolução incremental, baixo risco.
2. **Migrar para Clean Architecture com MediatR** — mais desacoplado, porém reescrita ampla.
3. **Manter como está** — menor esforço, mas acoplamento persiste.

### Decisão
Adotar a alternativa 1: manter as camadas existentes e introduzir `IRepository<T>` e `IUnitOfWork`.

### Consequências
- **Positivas:** testes de casos de uso com dublês; troca de persistência sem tocar em Application.
- **Negativas:** mais arquivos e indireção.
- **Mitigação:** interfaces por agregado, não por tabela, para evitar explosão de contratos.

---

## ADR-005 — Abstrair gateways de pagamento com `IPaymentGateway`

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
O produto precisa cobrar assinatura da plataforma e pagamentos dos clientes finais. O público brasileiro exige Pix; o público internacional favorece cartão via Stripe.

### Alternativas
1. **Stripe + Mercado Pago atrás de uma abstração** — cobre os dois públicos, mas exige dois integradores.
2. **Só Mercado Pago** — simples, foco no Brasil, sem cartão internacional nativo.
3. **Só Stripe** — padrão SaaS global, sem Pix nativo.

### Decisão
Adotar a alternativa 2: interface `IPaymentGateway` com implementação única `MercadoPagoGateway` (Pix, boleto e cartão). A abstração é mantida para permitir adicionar Stripe no futuro sem alterar casos de uso.

### Consequências
- **Positivas:** uma única integração para manter e testar; cobertura de Pix e boleto, essenciais no mercado brasileiro; menor complexidade de webhook.
- **Negativas:** sem cartão internacional nativo; dependência de um único provedor.
- **Mitigação:** contrato `IPaymentGateway` normalizado permite plugar outro provedor sem tocar em Application.

---

## ADR-006 — WhatsApp via Cloud API oficial da Meta com abstração

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
A automação de atendimento pelo WhatsApp é diferencial do produto. Existem a API oficial da Meta e provedores intermediários.

### Alternativas
1. **Cloud API oficial da Meta** — conformidade e estabilidade, porém exige verificação de número e aprovação de templates.
2. **Provedor intermediário (Twilio/Z-API)** — onboarding mais rápido, com custo por mensagem maior e dependência de terceiro.
3. **Sem WhatsApp no MVP** — reduz escopo, mas perde diferencial.

### Decisão
Adotar a alternativa 1, com interface `IMessagingProvider` para permitir troca.

### Consequências
- **Positivas:** conformidade com as políticas da Meta; base para escala.
- **Negativas:** processo de verificação e aprovação de templates pode atrasar a fase F6.
- **Mitigação:** iniciar a verificação do número na fase F0, em paralelo ao desenvolvimento.

---

## ADR-007 — Site gerado por IA sobre biblioteca de componentes controlada

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
A visão de produto exige geração de site por IA sem que o usuário domine editores visuais. Liberdade total de geração comprometeria integração, segurança e consistência.

### Alternativas
1. **IA seleciona e configura componentes da plataforma** — liberdade visual com garantias técnicas.
2. **IA gera HTML/CSS livre** — máxima flexibilidade, mas quebra integração e segurança.
3. **Editor visual manual** — controle total do usuário, mas exige aprendizado.

### Decisão
Adotar a alternativa 1: a IA opera sobre uma biblioteca de componentes mantida pela plataforma, configurando propriedades validadas por schema.

### Regra comercial do site
- **Todos os planos oferecem todos os serviços e módulos.** A diferenciação entre planos está na quantidade de profissionais e no **grau de personalização do site**.
- Planos mais caros permitem maior liberdade de modificação: Essencial (básica), Pro (intermediária), Premium (avançada) e Business (total).
- A **geração inicial e a publicação** estão incluídas na mensalidade do plano.
- **Alterações posteriores ao lançamento do site são cobradas à parte**, por rodada de alteração, independentemente do plano.

### Consequências
- **Positivas:** integração nativa com agenda e pagamentos; responsividade e acessibilidade garantidas; manutenção centralizada; receita recorrente complementada por cobrança de alterações.
- **Negativas:** limite de criatividade visual; necessidade de manter e evoluir a biblioteca; complexidade de controlar o grau de personalização por plano.
- **Mitigação:** ampliar a biblioteca conforme demanda real dos tenants; registrar cada alteração pós-lançamento como cobrança avulsa auditável.

---

## ADR-008 — Aprovação obrigatória de alterações geradas por IA

**Status:** Aceito
**Data:** 2026-11-04

### Contexto
A IA pode alterar conteúdo publicado. Publicação automática criaria risco reputacional e de informação incorreta.

### Alternativas
1. **Aprovação explícita antes da publicação** — segurança e controle do usuário.
2. **Publicação automática** — agilidade, com risco alto.
3. **Aprovação apenas para alterações grandes** — meio-termo com regra complexa.

### Decisão
Adotar a alternativa 1: toda alteração gera um `AiChangeSet` que precisa de `Approve` para chegar ao site publicado.

### Consequências
- **Positivas:** nenhuma informação não aprovada chega ao público; histórico de aprovações auditável.
- **Negativas:** um passo extra no fluxo do usuário.
- **Mitigação:** prévia lado a lado com aprovação em um clique.

---

## ADR-009 — Separação entre `User` (identidade) e `Customer` (relacionamento)

**Status:** Aceito
**Data:** 2026-11-04

### Contexto
No modelo atual, `User` acumula identidade de acesso e dados de cliente. Nem todo cliente final terá login, e um mesmo cliente pode ser atendido por vários tenants.

### Alternativas
1. **Separar `User` e `Customer`** — modelagem correta, com vínculo opcional.
2. **Manter `User` único** — simples, mas força login para todo cliente e mistura responsabilidades.
3. **`Customer` sem vínculo com `User`** — impede login do cliente.

### Decisão
Adotar a alternativa 1: `User` representa identidade de acesso; `Customer` representa o relacionamento comercial com o tenant; o vínculo é opcional.

### Consequências
- **Positivas:** cliente sem login cadastrado; histórico por tenant; base para LGPD por titular.
- **Negativas:** duas entidades para sincronizar quando o cliente cria conta.
- **Mitigação:** caso de uso de vinculação com validação de e-mail.

---

## ADR-010 — Generalizar `Barber` em `Professional`

**Status:** Aceito
**Data:** 2026-11-04

### Contexto
O modelo atual tem `Barber` com `BarberSchedule`, específico de barbearia. A plataforma atende salões, clínicas, estúdios de tatuagem e outros segmentos.

### Alternativas
1. **Generalizar em `Professional`** — uma entidade para todos os segmentos.
2. **Manter `Barber` e criar entidades por segmento** — duplicação de regras.
3. **Manter `Barber` com nome genérico** — dívida de nomenclatura.

### Decisão
Adotar a alternativa 1: `Professional` com `WorkSchedule`, mantendo as regras de jornada já testadas.

### Consequências
- **Positivas:** um modelo para todos os segmentos; regras de jornada reaproveitadas.
- **Negativas:** migration de dados de `Barber` para `Professional`.
- **Mitigação:** manter `BarberSchedule` como `WorkSchedule` com os mesmos testes.

---

## ADR-011 — Docker + GitHub Actions + PaaS na primeira fase

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
O projeto não possui containerização nem integração contínua. O SaaS precisa de deploy reproduzível e verificação automática.

### Alternativas
1. **Docker + GitHub Actions + PaaS** — menor esforço operacional, escala gerenciada.
2. **VPS própria com Docker** — controle total, com custo operacional de manutenção.
3. **Azure/AWS gerenciados** — recursos avançados, com complexidade e custo maiores no início.

### Decisão
Adotar a alternativa 1 na primeira fase, com possibilidade de migrar para nuvem gerenciada quando o volume exigir.

### Consequências
- **Positivas:** deploy rápido; pipeline garante build e testes; baixo custo inicial.
- **Negativas:** limites de escala do PaaS; dependência do provedor.
- **Mitigação:** manter imagens Docker portáveis para migração futura.

---

## ADR-012 — LGPD: tenant como controlador, plataforma como operadora

**Status:** ✅ Aceito
**Data:** 2026-11-04

### Contexto
A plataforma armazena dados pessoais de clientes finais de cada estabelecimento. É necessário definir quem responde pelo tratamento.

### Alternativas
1. **Tenant como controlador, plataforma como operadora** — modelo natural para SaaS B2B.
2. **Plataforma como controladora** — concentra responsabilidade, mas exige base legal própria para cada finalidade.
3. **Definir caso a caso** — insegurança jurídica.

### Decisão
Adotar a alternativa 1: cada estabelecimento é controlador dos dados dos seus clientes; a plataforma atua como operadora, seguindo instruções do tenant.

### Consequências
- **Positivas:** alinhamento com o modelo SaaS B2B; responsabilidade proporcional.
- **Negativas:** a plataforma precisa fornecer ferramentas de atendimento ao titular por tenant.
- **Mitigação:** implementar consentimento, exclusão e anonimização por tenant na fase F9.

---

## ADR-013 — Auditoria imutável por inserção

**Status:** Aceito
**Data:** 2026-11-04

### Contexto
Ações sensíveis precisam de rastreabilidade para suporte, segurança e conformidade.

### Alternativas
1. **Tabela de auditoria somente inserção** — imutabilidade simples e verificável.
2. **Auditoria com atualização** — permite correção, mas compromete a confiabilidade.
3. **Logs de aplicação apenas** — não estruturado para consulta.

### Decisão
Adotar a alternativa 1: `AuditEntry` sem métodos de alteração ou exclusão.

### Consequências
- **Positivas:** trilha confiável; consulta por entidade e por autor.
- **Negativas:** crescimento contínuo da tabela.
- **Mitigação:** particionamento por período e política de retenção.

---

## ADR-014 — Objeto de valor `Money` no lugar de `decimal` puro

**Status:** Aceito
**Data:** 2026-11-04

### Contexto
Preços, pagamentos e comissões circulam entre entidades. Usar `decimal` puro permite ambiguidade de moeda e valores negativos indevidos.

### Alternativas
1. **Objeto de valor `Money`** — encapsula valor e moeda com invariantes.
2. **`decimal` puro** — simples, mas sem proteção.
3. **`decimal` com sufixo no nome** — convenção frágil.

### Decisão
Adotar a alternativa 1: `Money` com `Amount` e `Currency`, rejeitando valor negativo na construção.

### Consequências
- **Positivas:** invariantes centralizadas; leitura do código mais clara.
- **Negativas:** mapeamento adicional no EF Core.
- **Mitigação:** owned type configurado no `DbContext`.
