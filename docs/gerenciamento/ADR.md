# Architecture Decision Records (ADR)

> **Status:** v3 — revisada em 2026-11-10 conforme `docs/diretrizes.md` e a política comercial de WhatsApp e IA. Todos os 21 ADRs estão aceitos.
> **Formato:** [ADR-NNN] Título — Status — Data.
> **Decisões:** confirmadas pelo responsável do produto; ver também `docs/funcionalidades.md` seção 0 e `docs/gerenciamento/ARCH.md` seção 13.

---

## Índice

| ADR | Título | Status |
| --- | --- | --- |
| ADR-001 | Construir o Servia como sistema novo, sem reaproveitar o código acadêmico | ✅ Aceito |
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
| ADR-015 | Identidade de usuário independente por tenant | ✅ Aceito |
| ADR-016 | Isolamento multi-tenant em todas as camadas | ✅ Aceito |
| ADR-017 | Controle de concorrência na reserva de horário | ✅ Aceito |
| ADR-018 | Núcleo universal com especialização por políticas | ✅ Aceito |
| ADR-019 | Operação e cobrança do WhatsApp como Solution Partner | ✅ Aceito |
| ADR-020 | Separação entre financeiro do tenant e billing da plataforma | ✅ Aceito |
| ADR-021 | Franquia mensal de IA e cobrança separada por recurso | ✅ Aceito |

---

## ADR-001 — Construir o Servia como sistema novo, sem reaproveitar o código acadêmico

**Status:** ✅ Aceito
**Data:** 2026-11-04 (revisado em 2026-11-10)

### Contexto
O projeto nasceu como aplicação acadêmica de uma barbearia, com backend .NET 10 em camadas, frontend React e regras de domínio testadas. A visão de produto exige uma plataforma SaaS multi-segmento, multi-tenant, com site por tenant, billing, WhatsApp e IA. Existem três caminhos: reaproveitar o código, construir um sistema novo ou manter dois produtos.

### Alternativas
1. **Reaproveitar como primeiro tenant** — menor esforço inicial, mas exige refatorar todas as entidades para multi-tenancy, substituir a persistência, reescrever o frontend por superfícies de produto e arrastar decisões do modelo single-shop.
2. **Construir um sistema novo** — modelo desenhado desde o início para multi-tenancy e multi-segmento, sem dívida herdada.
3. **Manter dois produtos** — duplica manutenção e divergência de código.

### Decisão
Adotar a alternativa 2: **o Servia é um sistema novo**. O código acadêmico **não** é reaproveitado como base do SaaS.

O novo sistema é construído a partir dos documentos de `docs/` e `docs/gerenciamento/`, que definem o domínio, o modelo de classes, a arquitetura e o roadmap.

**Remoção do código acadêmico (2026-11-10):** as pastas `backend/` (projetos `BackAndre.*`) e `frontend/` foram **removidas do repositório**. O código cumpriu seu papel como origem do projeto e não tinha mais função: nenhum artefato do Servia o referenciava, e mantê-lo criava ambiguidade sobre qual código é a base do produto.

A memória da origem permanece em `docs/levantamento_lacunas.md`, que registra o que existia e por que foi descartado. A identificação acadêmica (instituição, disciplina, professor e integrantes) permanece no `README.md`, como requisito da disciplina.

### Consequências
- **Positivas:** modelo desenhado para multi-tenancy desde a primeira linha; nenhuma refatoração de entidades legadas; liberdade para aplicar corretamente os conceitos de POO exigidos pela disciplina; sem risco de arrastar o modelo single-shop.
- **Negativas:** o trabalho funcional existente não é aproveitado; a primeira entrega parte de uma base vazia.
- **Mitigação:** o roadmap organiza a construção em fases (F0–F9), e os critérios de avaliação da disciplina orientam a qualidade do código novo desde o início.
- **Remoção:** o código acadêmico foi removido em 2026-11-10. Recuperá-lo é possível pelo histórico do Git, no commit anterior à remoção.

> **Nota de revisão:** esta decisão substitui a versão anterior, que previa reaproveitar o código como primeiro tenant. A mudança foi tomada pelo responsável do produto em 2026-11-10, por considerar a construção de um sistema novo mais eficiente do que adaptar o existente.

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
- **Positivas:** um modelo para todos os segmentos; regras de jornada definidas uma única vez.
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

---

## ADR-015 — Identidade de usuário independente por tenant

**Status:** ✅ Aceito
**Data:** 2026-11-10
**Origem:** `docs/diretrizes.md` §3.1

### Contexto
A mesma pessoa física pode frequentar vários estabelecimentos. É preciso decidir se existe uma identidade global compartilhada entre tenants ou cadastros independentes por estabelecimento.

### Alternativas
1. **Cadastros independentes por tenant** — reforça o isolamento; a mesma pessoa tem registros distintos em cada estabelecimento.
2. **Identidade global compartilhada** — um único cadastro serve todos os tenants, mas cria uma entidade que atravessa a fronteira de isolamento.
3. **Identidade global com vínculo opcional** — meio-termo, com complexidade de sincronização e risco de vazamento por associação.

### Decisão
Adotar a alternativa 1: os usuários são cadastrados individualmente em cada estabelecimento. Não existe identidade global, não há vínculo automático entre registros e o e-mail é único apenas dentro do tenant.

### Consequências
- **Positivas:** isolamento reforçado; nenhuma entidade global atravessa a fronteira dos tenants; modelagem mais simples.
- **Negativas:** a mesma pessoa precisa se cadastrar novamente em outro estabelecimento; não há histórico unificado entre tenants.
- **Mitigação:** o cadastro por tenant é rápido e o cliente final pode ser criado pela equipe sem login.

---

## ADR-016 — Isolamento multi-tenant em todas as camadas

**Status:** ✅ Aceito
**Data:** 2026-11-10
**Origem:** `docs/diretrizes.md` §3

### Contexto
O isolamento entre tenants é requisito fundamental e inegociável. Um filtro global de consulta protege a leitura, mas não impede escrita cruzada nem associação indevida entre registros de tenants diferentes.

### Alternativas
1. **Isolamento em todas as camadas** — resolução no servidor, validação de pertencimento, verificação na escrita e FK composta no banco.
2. **Apenas filtro global de consulta** — simples, mas deixa a escrita e as associações desprotegidas.
3. **Isolamento apenas na aplicação** — depende inteiramente da disciplina do código.

### Decisão
Adotar a alternativa 1: o tenant é resolvido no servidor; `ITenantMembershipValidator` confirma o pertencimento do usuário; a escrita verifica `TenantId`; e as chaves estrangeiras entre entidades de negócio são **compostas com `TenantId`**, fazendo o banco rejeitar associações cruzadas.

### Consequências
- **Positivas:** vazamento por escrita torna-se impossível no nível do banco; defesa em profundidade.
- **Negativas:** chaves estrangeiras compostas aumentam a complexidade das migrations e dos índices.
- **Mitigação:** convenção única aplicada a todas as entidades de negócio; testes de isolamento obrigatórios no pipeline.

---

## ADR-017 — Controle de concorrência na reserva de horário

**Status:** ✅ Aceito
**Data:** 2026-11-10
**Origem:** `docs/diretrizes.md` §4.1

### Contexto
Duas solicitações simultâneas podem reservar o mesmo horário se a disponibilidade for verificada apenas por consulta antes da gravação. O resultado seria agenda inconsistente e dois clientes no mesmo horário.

### Alternativas
1. **Trava de reserva com índice único parcial + transação** — o banco garante uma única reserva.
2. **Apenas consulta prévia de disponibilidade** — simples, mas sujeita a corrida.
3. **Bloqueio pessimista de tabela** — correto, porém com contenção alta.

### Decisão
Adotar a alternativa 1: a reserva adquire um `ScheduleLock` dentro de transação; o índice único parcial em `(TenantId, ProfessionalId, Date, StartTime)` faz a segunda transação concorrente falhar. A trava tem expiração para não bloquear a agenda em caso de falha.

### Consequências
- **Positivas:** consistência garantida pelo banco; funciona com múltiplas instâncias da API.
- **Negativas:** uma tabela adicional e tratamento explícito do conflito na aplicação.
- **Mitigação:** teste de concorrência que dispara N reservas paralelas e exige exatamente 1 sucesso.

---

## ADR-018 — Núcleo universal com especialização por políticas

**Status:** ✅ Aceito
**Data:** 2026-11-10
**Origem:** `docs/diretrizes.md` §2

### Contexto
Os segmentos compartilham a maior parte das funcionalidades, mas têm particularidades. Implementar cada segmento como aplicação independente duplicaria regras; concentrar condicionais de segmento no núcleo tornaria o código frágil e difícil de evoluir.

### Alternativas
1. **Núcleo universal + políticas injetadas** — o núcleo depende de contratos; a implementação é escolhida por configuração.
2. **Condicionais de segmento no núcleo** — rápido no início, mas acumula complexidade.
3. **Aplicação independente por segmento** — isolamento total, com duplicação massiva.

### Decisão
Adotar a alternativa 1: `IBookingPolicy`, `IAvailabilityPolicy`, `ICommissionPolicy` e `IBenefitPolicy` são injetadas no núcleo. O núcleo não consulta o segmento para decidir comportamento.

### Consequências
- **Positivas:** reutilização máxima; novo segmento entra por configuração ou nova política; núcleo testável isoladamente.
- **Negativas:** mais abstrações e um passo de configuração por tenant.
- **Mitigação:** manter o conjunto de políticas pequeno e só criar nova política com justificativa de reuso.

---

## ADR-019 — Operação e cobrança do WhatsApp como Solution Partner

**Status:** ✅ Aceito
**Data:** 2026-11-10 (revisado)
**Origem:** `docs/diretrizes.md` §7.1 + política comercial definida em 2026-11-10

### Contexto
O WhatsApp tem custo por mensagem. Cada estabelecimento poderia contratar a Meta diretamente, mas isso fragmentaria a operação, dificultaria o suporte e impediria o Servia de controlar o consumo. É preciso decidir quem opera a integração, quem paga a Meta e como o custo é repassado ao estabelecimento.

### Alternativas
1. **Servia como Solution Partner, com consumo tarifado** — operação centralizada, pagamento único à Meta, repasse por consumo.
2. **Cada tenant contrata a Meta diretamente** — sem intermediação, mas sem controle de consumo e com suporte fragmentado.
3. **Servia subsidia mensagens dentro dos planos** — previsível para o cliente, mas transfere ao Servia um custo variável e ilimitado.

### Decisão
Adotar a alternativa 1:

- O Servia **administra a integração e a operação** do WhatsApp e **centraliza o pagamento à Meta** pelo modelo **Solution Partner**, condicionado a **aprovação** e **linha de crédito**.
- Cada estabelecimento **paga pelo consumo tarifado que gerar**; **não há subsídio** de mensagens com custo dentro dos planos.
- O **uso gratuito da Meta é aproveitado dentro dos planos** do Servia — a franquia gratuita é repassada ao tenant, não retida pela plataforma.
- O estabelecimento escolhe entre **cobrança automática** e **compra antecipada de créditos**.
- O estabelecimento define **limites de consumo** e **permissões por tipo de mensagem**.
- Todo envio passa por `IUsageQuotaService.TryConsume` antes de chamar o provedor; ao atingir o limite, o envio é bloqueado no servidor.

### Consequências
- **Positivas:** custo variável não fica com o Servia; operação e suporte centralizados; controle granular por tenant; transparência de consumo.
- **Negativas:** dependência de aprovação e de linha de crédito junto à Meta; o Servia assume risco de crédito entre o consumo e o repasse.
- **Mitigação:** iniciar a aprovação na fase F0; monitorar o consumo agregado contra a linha de crédito; bloquear no servidor ao atingir o limite do tenant.

---

## ADR-020 — Separação entre financeiro do tenant e billing da plataforma

**Status:** ✅ Aceito
**Data:** 2026-11-10
**Origem:** `docs/diretrizes.md` §8

### Contexto
A plataforma tem duas relações financeiras distintas: o estabelecimento administra as finanças do próprio negócio, e o Servia cobra a assinatura do estabelecimento. Misturar as duas cria ambiguidade de titularidade, de relatórios e de regras.

### Alternativas
1. **Dois contextos independentes** — responsabilidades, regras e registros separados.
2. **Contexto financeiro único** — menos tabelas, mas mistura titularidades distintas.
3. **Billing dentro do financeiro do tenant** — confunde receita do negócio com custo da plataforma.

### Decisão
Adotar a alternativa 1: o financeiro do tenant (`Payment`, `Transaction`, `CommissionRule`, `CommissionEntry`) e o billing da plataforma (`Subscription`, `PlatformInvoice`, `SiteChangeCharge`, `UsageQuota`) são domínios separados, sem referência cruzada entre agregados.

### Consequências
- **Positivas:** relatórios corretos por titular; falha em um contexto não corrompe o outro; evolução independente.
- **Negativas:** dois conjuntos de agregados e de relatórios.
- **Mitigação:** ambos usam o mesmo `IPaymentGateway`, mas com contratos e registros separados.

---

## ADR-021 — Franquia mensal de IA e cobrança separada por recurso

**Status:** ✅ Aceito
**Data:** 2026-11-10
**Origem:** política comercial definida em 2026-11-10

### Contexto
A geração de site por IA tem custo variável por requisição. Incluir IA ilimitada em todos os planos transferiria ao Servia um custo imprevisível; cobrar por requisição desde a primeira geraria atrito no onboarding. Além disso, WhatsApp e IA têm custos de naturezas diferentes e precisam ser compreendidos separadamente pelo estabelecimento.

### Alternativas
1. **Franquia mensal por plano + excedente cobrado + linhas separadas** — previsível para o cliente, sustentável para o Servia e transparente.
2. **IA ilimitada em todos os planos** — simples, mas com custo variável ilimitado para o Servia.
3. **Cobrança por requisição desde a primeira** — sem risco financeiro, mas com atrito no onboarding.

### Decisão
Adotar a alternativa 1:

- Cada plano tem **franquia mensal própria de IA** (`Plan.IncludedAiUnits`), crescente por categoria.
- O consumo **excedente à franquia é cobrado adicionalmente**.
- A franquia de IA é **independente** da franquia de WhatsApp.
- Os custos de **WhatsApp** e de **IA** são **contabilizados e cobrados separadamente**, em linhas distintas da fatura.
- Cada linha é rastreável até o consumo que a originou, e o tenant visualiza o detalhamento por recurso.

### Consequências
- **Positivas:** custo previsível para o cliente; risco financeiro controlado; transparência que permite ao tenant controlar gastos por recurso.
- **Negativas:** mais uma dimensão de configuração por plano; exige apuração precisa por recurso.
- **Mitigação:** `IConsumptionBillingService` apura cada recurso isoladamente; aviso antes de consumir excedente; detalhamento por recurso na fatura.
