# Servia — Descrição do Produto

> **Finalidade:** documento único de visão e princípios do produto. Consolida a descrição original do produto e as diretrizes aprovadas ao longo do desenvolvimento.
> **Escopo:** visão, objetivos, proposta de valor, público, princípios de produto e princípios arquiteturais que influenciam o produto.
> **Fora de escopo:** catálogo de funcionalidades (ver [`funcionalidades.md`](funcionalidades.md)), arquitetura técnica (ver [`../arquitetura/ARCH.md`](../arquitetura/ARCH.md)) e decisões registradas (ver [`../arquitetura/ADR.md`](../arquitetura/ADR.md)).

---

## 1. Visão e objetivos

### 1.1. O que é o Servia

O Servia é uma plataforma SaaS multi-tenant de gestão, atendimento e presença digital para negócios que trabalham com serviços agendados. Cada estabelecimento opera em seu próprio ambiente isolado, com site individual, agenda, equipe, catálogo, financeiro, comunicação e billing da plataforma — tudo sobre uma infraestrutura compartilhada.

### 1.2. Objetivo

Eliminar a fragmentação da gestão entre agendas, planilhas, conversas no WhatsApp, ferramentas financeiras e sistemas independentes, oferecendo a cada estabelecimento um ambiente próprio construído sobre uma base comum.

### 1.3. Proposta de valor

> **Tudo o que seu negócio precisa para atender, organizar e crescer em uma única plataforma.**

A plataforma combina uma estrutura central robusta com flexibilidade para atender às particularidades de diferentes atividades. A inteligência artificial tem papel central na simplificação da experiência, especialmente na criação e manutenção de sites, permitindo que empreendedores obtenham presença digital personalizada sem dominar ferramentas técnicas.

### 1.4. Público-alvo

Empresas e profissionais independentes que oferecem serviços mediante agendamento:

- Salões de beleza e cabeleireiros.
- Barbearias.
- Manicure, pedicure e nail design.
- Estética facial e corporal.
- Design de sobrancelhas e extensão de cílios.
- Tatuagem e piercing.
- Massoterapia e bem-estar.
- Maquiagem.
- Depilação.
- Tranças e tratamentos capilares.
- Outros negócios com atendimento agendado.

A arquitetura permite a expansão para novos segmentos sem reconstruir a plataforma.

---

## 2. Conceito central: uma plataforma, diferentes segmentos

O produto é desenvolvido sobre uma plataforma central compartilhada, capaz de atender a diferentes segmentos sem criar e manter um sistema independente para cada atividade.

A estrutura é modular e adaptável: todos os negócios utilizam uma base comum de funcionalidades, enquanto configurações, módulos opcionais e recursos específicos adequam a experiência às características de cada atividade.

Essa abordagem concilia dois objetivos:

- **Padronização:** manter uma estrutura central consistente, reduzindo a complexidade de desenvolvimento, manutenção e evolução.
- **Flexibilidade:** permitir que cada estabelecimento utilize ferramentas, configurações e fluxos adequados à sua operação.

### 2.1. Organização das funcionalidades

**Funcionalidades universais** — base comum utilizada por todos os segmentos: cadastro e gestão de clientes, catálogo de serviços e preços, gestão de profissionais e equipes, agendamento e disponibilidade, site individual, comunicação e notificações, registro de pagamentos, gestão financeira e relatórios.

**Módulos opcionais** — habilitados conforme o modelo de negócio: clubes de assinatura, pacotes de serviços, cobranças recorrentes, comissões e remuneração, automações de atendimento, recursos de relacionamento e fidelização.

**Configurações e adaptações por segmento** — ajustes nos processos e funcionalidades para atender às particularidades de cada atividade, sem exigir uma aplicação independente.

### 2.2. Núcleo universal e particularidades

O núcleo do sistema é universal e **não contém condicionais que identifiquem o segmento**. As particularidades são implementadas por **módulos, políticas de negócio e comportamentos especializados**, acessados por interfaces e polimorfismo.

Uma configuração nova só entra se puder ser reutilizada por mais de um segmento. Um `if (tenant.Segment == ...)` no núcleo é um defeito de arquitetura.

---

## 3. Principais soluções do produto

### 3.1. Site próprio e presença digital com inteligência artificial

Cada estabelecimento conta com um site individual, personalizado de acordo com sua identidade visual, atividade, serviços, profissionais e posicionamento comercial.

**Criação inicial.** A IA utiliza as informações do estabelecimento — nome, segmento, descrição, serviços, profissionais, localização, logotipo e fotografias — para planejar e gerar um site completo: estrutura das páginas, organização das seções, identidade visual e conteúdos iniciais.

**Criação baseada em referências.** O usuário pode fornecer referências visuais (endereços de sites, imagens, exemplos de identidade) que a IA analisa como inspiração.

**Personalização conversacional.** Após a criação inicial, o usuário solicita alterações por conversação natural: cores, apresentação dos serviços, destaque de profissionais, reorganização de seções, textos e imagens. A IA interpreta a solicitação e disponibiliza nova prévia.

**Arquitetura controlada por componentes.** Os sites são gerados a partir de uma biblioteca de componentes visuais e funcionais mantida pela plataforma. A IA seleciona, combina, configura e organiza esses componentes, sem alterar o funcionamento dos recursos essenciais. A IA **não gera código** e **não inventa** informações comerciais, preços, serviços, qualificações ou avaliações.

**Visualização e publicação.** O usuário visualiza o site e solicita quantas alterações forem necessárias antes da publicação. As modificações só entram no ar após **aprovação explícita**. Cada estabelecimento tem um endereço individual fornecido pela plataforma, com possibilidade de domínio próprio.

### 3.2. Agendamento online e gestão de agenda

Sistema de agendamento integrado à disponibilidade dos profissionais e aos serviços oferecidos. Os clientes consultam horários e agendam a qualquer momento; o estabelecimento administra a agenda, organiza a equipe, controla a disponibilidade e acompanha os atendimentos.

### 3.3. Atendimento e automação pelo WhatsApp

Integração com o WhatsApp para automatizar conversas e processos de atendimento: responder dúvidas frequentes, apresentar serviços, consultar disponibilidade, auxiliar em agendamentos, enviar confirmações e lembretes, comunicar alterações e realizar ações de relacionamento.

> **Decisão comercial vigente (2026-11-14):** o estabelecimento é **titular da própria conta WhatsApp Business Platform** e **paga diretamente à Meta**. O Servia fornece a integração, a orientação e as estimativas de custo, mas **não intermedia a cobrança**, não vende créditos e não mantém carteira ou franquia de WhatsApp. A integração está **pausada** e não é atividade de implementação imediata. Ver §5.4.

### 3.4. Gestão de clientes e relacionamento

Cadastro e histórico centralizado dos clientes, com informações sobre atendimentos, serviços contratados, preferências, pagamentos, agendamentos e relacionamento com o estabelecimento. Recursos de comunicação e fidelização permitem manter contato, divulgar novidades, recuperar clientes inativos e incentivar novos agendamentos.

### 3.5. Gestão operacional e financeira

Ferramentas para administrar serviços e preços, profissionais e permissões, agendas e disponibilidade, atendimentos e histórico, pagamentos, controle financeiro (receitas, despesas, comissões) e relatórios e indicadores.

### 3.6. Assinaturas, clubes e pacotes de serviços

Recursos para comercializar serviços por modelos recorrentes ou pré-pagos: clubes de assinatura, planos mensais e pacotes com quantidades determinadas de sessões. A plataforma controla benefícios contratados, utilização, períodos de validade, cobranças recorrentes e acompanhamento das assinaturas.

### 3.7. Pagamentos e transações

Integração de pagamentos online para que os clientes paguem no momento do agendamento ou conforme as condições do negócio. A plataforma também registra pagamentos presenciais, mantendo o histórico financeiro e operacional centralizado.

---

## 4. Princípios e diretrizes de produto

### 4.1. Isolamento entre estabelecimentos

O isolamento entre tenants é requisito **fundamental e inegociável**. Nenhum usuário de um estabelecimento acessa dados de outro, independentemente de papel, nível de autorização ou privilégio dentro do próprio tenant.

### 4.2. Identidade independente por estabelecimento

Os usuários são cadastrados individualmente em cada estabelecimento. A mesma pessoa física que frequenta dois estabelecimentos possui **dois cadastros independentes**, sem identidade global e sem vínculo automático entre eles. Isso reforça o isolamento e evita manter uma identidade compartilhada.

### 4.3. Agendamento baseado na disponibilidade individual

O agendamento funciona com base na disponibilidade de cada prestador de serviço: jornada de trabalho, horários disponíveis, bloqueios, duração do serviço, cancelamentos e remarcações. Uma reserva torna o período indisponível para agendamentos incompatíveis.

**Concorrência:** a verificação de disponibilidade não pode depender exclusivamente de uma consulta prévia. Estratégias de controle de concorrência garantem que apenas uma operação incompatível seja confirmada, mesmo sob solicitações simultâneas.

### 4.4. Personalização controlada do site

O backend permanece independente das escolhas de personalização visual. As alterações do estabelecimento afetam apenas a configuração e a apresentação do site, nunca a estrutura da aplicação.

### 4.5. IA como mecanismo de personalização

A IA interpreta as preferências do proprietário e configura os modelos existentes. Ela **não gera código React**, **não altera arquivos do projeto** e **não modifica a estrutura da aplicação**. As operações da IA são passíveis de visualização, revisão e aprovação antes de serem publicadas.

### 4.6. Separação entre financeiro do estabelecimento e billing da plataforma

Existem **dois contextos financeiros independentes**, com responsabilidades, regras e registros próprios:

- **Financeiro do estabelecimento** — gestão das finanças do próprio negócio: receitas, despesas, pagamentos, comissões, pacotes, clubes, relatórios.
- **Billing da plataforma** — relação comercial entre o Servia e os estabelecimentos: planos, assinaturas, cobranças recorrentes, inadimplência, histórico.

Nenhum dos dois referencia o outro em suas regras de negócio. Uma falha em um contexto não corrompe o outro.

### 4.7. Integrações como parte do produto

WhatsApp e pagamentos integram a solução, com controle de utilização, custos e tratamento de falhas. As integrações são acessadas por abstrações, permitindo trocar de provedor sem alterar os casos de uso.

### 4.8. Confiabilidade e consistência

Regras de negócio, isolamento de dados, agendamentos e integrações possuem mecanismos de validação e testes automatizados. O comportamento permanece consistente mesmo diante de falhas e situações excepcionais.

### 4.9. Planos comerciais

**Todos os planos oferecem todos os serviços e módulos.** A diferenciação está na quantidade de profissionais incluídos, no grau de personalização do site e na franquia mensal de IA.

| Plano | Profissionais incluídos | Personalização do site | Franquia de IA/mês |
| --- | --- | --- | --- |
| Essencial | até 3 | Básica | 100 créditos (1 milhão de tokens) |
| Pro | até 7 | Intermediária | 500 créditos (5 milhões de tokens) |
| Premium | até 15 | Avançada | 1.500 créditos (15 milhões de tokens) |
| Business | ilimitado | Total | 5.000 créditos (50 milhões de tokens) |

> **Fonte única dos valores comerciais:** o código (`PlanSeeder`) e o catálogo em [`funcionalidades.md`](funcionalidades.md) §0.1. Este documento apresenta a tabela apenas como referência de visão; em caso de divergência, prevalecem o código e o catálogo.

**Regra de profissionais:** não há taxa por profissional excedente. Ao ultrapassar o limite, o tenant precisa **subir de categoria**.

**Regra de site:** a geração inicial e a publicação estão incluídas no plano. **Alterações posteriores ao lançamento são cobradas à parte**, conforme a tabela aprovada em [`funcionalidades.md`](funcionalidades.md) §0.3.

### 4.10. Trial e inadimplência

- Trial de **14 dias** com cartão de crédito cadastrado; ao fim do trial, o cartão é debitado automaticamente.
- Inadimplência: **3 dias de aviso** com acesso total, seguidos de **bloqueio de escrita**.
- Durante o bloqueio, leitura e agenda continuam disponíveis por **15 dias**.
- Após 15 dias de bloqueio, o tenant é suspenso por completo.

---

## 5. Princípios arquiteturais que influenciam o produto

### 5.1. Multi-tenancy por banco único

Banco único com `TenantId` em todas as entidades de negócio, filtro global no EF Core, chaves estrangeiras compostas e testes de isolamento. O tenant vem **exclusivamente** da identidade autenticada — nunca de um valor enviado pelo cliente.

### 5.2. PostgreSQL como banco único

PostgreSQL 17 é o único provider, em desenvolvimento, testes e produção. Testar em outro banco mascararia diferenças de FK, tradução de consultas e semântica de texto.

### 5.3. Papéis e permissões

- **Owner** — titular da conta, criado automaticamente no cadastro. Possui todas as permissões administrativas do estabelecimento e as permissões exclusivas de titularidade, incluindo a contratação e administração do plano (`ManageBilling`).
- **Administrator** — administra a operação do estabelecimento (equipe, serviços, clientes, agenda, configurações, financeiro interno), mas **não** a relação comercial com o Servia.
- **Receptionist** — permissões operacionais superiores às do profissional comum, especialmente na administração da agenda de qualquer profissional; não gerencia a equipe.
- **Professional** — consulta e administra exclusivamente os próprios compromissos.
- **CustomRole** — papéis personalizados por estabelecimento, com permissões atribuíveis, sem acesso a permissões exclusivas do Owner ou da plataforma.
- **PlatformAdmin** — administração da plataforma, com privilégios distintos das permissões de tenant.

**Papel de acesso e atuação como prestador de serviços são dimensões independentes.** Um administrador que atende ocupa vaga no plano; um profissional que não atende não ocupa.

### 5.4. WhatsApp: decisão comercial e integração futura

**Decisão aprovada (2026-11-14):**

- O estabelecimento é **titular da própria conta** WhatsApp Business Platform.
- O estabelecimento **configura o pagamento diretamente na Meta**.
- O Servia fornece a **integração e a experiência de uso**, quando disponível.
- O Servia **pode orientar** sobre configuração, categorias, políticas e custos.
- O Servia **pode apresentar previsões estimadas** de custo, deixando explícito que a cobrança efetiva é determinada pela Meta.
- O Servia **não apresenta estimativas próprias como faturas** ou valores efetivamente cobrados.
- **Não há** franquia, créditos, carteira, consumo tarifado ou repasse intermediado pelo Servia.

**Situação:** a integração está **pausada**, condicionada à viabilidade e aos requisitos de acesso da Meta. Não é atividade de implementação imediata.

**Requisitos futuros, quando o trabalho for retomado:** conexão opcional da conta, orientação ao cliente, estimativa de custo, acompanhamento operacional e administração da conexão. Cuidados arquiteturais: isolamento por tenant, proteção de credenciais e validação de webhooks.

### 5.5. Consumo de IA

- **1 crédito = 10.000 tokens**, considerando a soma de tokens de entrada e saída informada pelo provedor.
- O consumo é acumulado com base no **total real**, sem arredondamento por operação.
- A franquia é **mensal**.
- Ao esgotar a franquia, o comportamento padrão é **bloquear** novas operações e **notificar** o cliente.
- O cliente pode **comprar créditos adicionais manualmente** ou **autorizar previamente a aquisição automática** de pacotes definidos.
- **Não existe** autorização para cobrança ilimitada ou cobrança automática por cada token excedente.

> **Estado de implementação:** a franquia, o bloqueio e o consumo em tokens estão implementados. A compra adicional, a aquisição automática e as notificações completas **ainda não estão integralmente implementadas**. A política aprovada está documentada separadamente do estado de implementação.

### 5.6. Expediente do estabelecimento

O expediente é configurável pelo administrador, com horário por dia da semana e dias que podem estar fechados. **Independe dos profissionais:** agendamentos dependem exclusivamente da jornada, dos bloqueios e da duração do serviço do profissional. Sem configuração, o estabelecimento é tratado como **fechado 24/7**, sem restringir agendamentos.

### 5.7. Testes e confiabilidade

A qualidade é verificada por testes automatizados em diferentes níveis: domínio (invariantes), aplicação (casos de uso com dublês), integração (repositórios, filtro de tenant, migrations), contrato (provedores externos), segurança (isolamento cross-tenant), concorrência (reservas simultâneas) e integrações (falha, indisponibilidade, repetição).

---

## 6. Diretrizes estratégicas de desenvolvimento

1. **Núcleo universal:** funcionalidades compartilhadas implementadas de forma reutilizável, sem aplicações independentes por segmento.
2. **Especialização por segmento:** particularidades tratadas por módulos, políticas de negócio e comportamentos especializados, com abstrações e polimorfismo.
3. **Isolamento absoluto entre tenants:** nenhum usuário acessa dados de outro estabelecimento.
4. **Identidade independente por estabelecimento:** cadastros específicos de cada tenant.
5. **Personalização controlada:** sites construídos a partir de modelos predefinidos, com personalização baseada em configurações.
6. **IA como mecanismo de personalização:** a IA auxilia na interpretação das preferências e na configuração dos modelos existentes, sem gerar ou modificar código.
7. **Separação de contextos financeiros:** financeiro do estabelecimento e billing da plataforma como domínios independentes.
8. **Integrações como parte do produto:** WhatsApp e pagamentos integram a solução, com controle de utilização, custos e tratamento de falhas.
9. **Confiabilidade e consistência:** regras, isolamento, agendamentos e integrações com validação e testes automatizados.

---

## 7. Fora de escopo da v1

- Aplicativo móvel nativo (iOS/Android).
- Marketplace público de estabelecimentos.
- Integrações com ERPs e sistemas fiscais.
- Emissão de nota fiscal.
- Multi-idioma e multi-moeda.
- Programa de indicação entre tenants.

---

## 8. Documentos relacionados

| Assunto | Documento |
| --- | --- |
| Catálogo de funcionalidades, planos e regras comerciais | [`funcionalidades.md`](funcionalidades.md) |
| Arquitetura de solução | [`../arquitetura/ARCH.md`](../arquitetura/ARCH.md) |
| Modelo de classes | [`../arquitetura/classes.md`](../arquitetura/classes.md) |
| Decisões arquiteturais e comerciais | [`../arquitetura/ADR.md`](../arquitetura/ADR.md) |
| Fases e estado de execução | [`../gerenciamento/Roadmap.md`](../gerenciamento/Roadmap.md) |
| Operação e incidentes | [`../operacao/RUNBOOK.md`](../operacao/RUNBOOK.md) |
| Colocar o primeiro cliente no ar | [`../operacao/ONBOARDING.md`](../operacao/ONBOARDING.md) |