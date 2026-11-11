# Funcionalidades — Plataforma SaaS de Gestão e Atendimento para Negócios de Serviços

> **Status:** v2 — revisada em 2026-11-10 conforme `docs/diretrizes.md`.
> **Fonte:** `docs/descricao_produto.md` (visão de produto) + `docs/diretrizes.md` (diretrizes e conclusões).
> **Registro das decisões:** ver seção 0 e `docs/gerenciamento/ADR.md`.

---

## 0. Premissas estruturais adotadas

| # | Decisão | Escolha adotada | Status |
| --- | --- | --- | --- |
| P1 | Relação com o código atual | O Servia é um **sistema novo**, construído a partir destes documentos. O código acadêmico **não** é reaproveitado e foi removido do repositório em 2026-11-10 | ✅ confirmado (revisado em 2026-11-10) |
| P2 | Multi-tenancy | **Banco único** com `TenantId` em todas as entidades de negócio + filtro global no EF Core | ✅ confirmado |
| P3 | Banco de produção | **PostgreSQL**; SQLite apenas em testes locais | ✅ confirmado |
| P4 | Escopo do MVP | **Escopo completo da descrição de produto** (fases F0–F9) | ✅ confirmado |
| P5 | Gateways de pagamento | **Somente Mercado Pago** (Pix, boleto e cartão), atrás da abstração `IPaymentGateway` | ✅ confirmado |
| P6 | WhatsApp | **Cloud API oficial da Meta**, com camada de abstração para trocar de provedor | ✅ confirmado |
| P7 | IA de site | LLM via API + biblioteca de componentes controlada pela plataforma; **provedor a definir na fase F8** | ✅ confirmado |
| P8 | Infra | Docker + GitHub Actions + PaaS na primeira fase | ✅ confirmado |
| P9 | LGPD | Cada estabelecimento é **controlador** dos dados dos seus clientes finais; a plataforma é **operadora** | ✅ confirmado |
| P10 | Identidade dos usuários | **Cadastros independentes por tenant**: a mesma pessoa física tem registros distintos em cada estabelecimento, sem identidade global e sem vínculo automático entre eles | ✅ confirmado (diretrizes §3.1) |
| P11 | Núcleo universal | Funcionalidades compartilhadas num núcleo único; particularidades por **módulos, políticas de negócio e polimorfismo**, nunca por condicionais de segmento no núcleo | ✅ confirmado (diretrizes §2) |
| P12 | Concorrência no agendamento | A disponibilidade **não** é validada apenas por consulta prévia; a reserva usa controle de concorrência no banco | ✅ confirmado (diretrizes §4.1) |
| P13 | Contextos financeiros | **Financeiro do estabelecimento** e **billing da plataforma** são domínios independentes, com regras e registros próprios | ✅ confirmado (diretrizes §8) |
| P14 | Operação do WhatsApp | O Servia **centraliza** a integração e a operação do WhatsApp no modelo **Solution Partner**, pagando a Meta diretamente | ✅ confirmado |
| P15 | Consumo tarifado | Cada estabelecimento paga pelo consumo que gerar; **não há subsídio** de mensagens com custo dentro dos planos | ✅ confirmado |
| P16 | Franquia de IA | A IA tem **franquia mensal própria por plano**, com cobrança adicional pelo excedente | ✅ confirmado |
| P17 | Cobrança separada | Custos de WhatsApp e de IA são contabilizados e cobrados **separadamente** | ✅ confirmado |

### 0.1. Modelo de planos

**Todos os planos oferecem todos os serviços e módulos.** A diferença entre planos está na quantidade de profissionais incluídos, no grau de personalização do site e nas **franquias de consumo** de WhatsApp e IA.

| Plano | Profissionais incluídos | Módulos | Personalização do site | Franquia de IA/mês | Observação |
| --- | --- | --- | --- | --- | --- |
| Essencial | até 3 | Todos | Básica — ajustes limitados de tema e conteúdo | faixa base | Entrada |
| Pro | até 7 | Todos | Intermediária — mais seções e variações visuais | faixa intermediária | Mais vendido |
| Premium | até 15 | Todos | Avançada — ampla liberdade de layout e identidade | faixa ampliada | Completo |
| Business | ilimitado | Todos | Total — máxima liberdade de personalização | faixa máxima | Grandes operações |

> **Distinção importante:** franquia de consumo **não** é bloqueio de módulo. Todos os planos têm acesso a WhatsApp e IA; o que varia é a quantidade incluída antes de passar a cobrar consumo excedente. Nenhum módulo é desabilitado por plano.

**Regra de cobrança:** a mensalidade do plano já inclui a quantidade de profissionais indicada. Não existe taxa por profissional excedente — ao ultrapassar o limite, o tenant precisa **subir de categoria**.

**Regra de geração e personalização do site:**

- A geração inicial do site é feita por IA conforme o plano contratado: planos mais caros permitem maior grau de modificação.
- O plano define o **limite de personalização** disponível (tema, seções, layout e identidade visual).
- **Alterações posteriores ao lançamento do site são cobradas à parte**, independentemente do plano. O tenant paga por cada rodada de alteração solicitada após a publicação.
- A primeira geração e a publicação inicial estão incluídas na mensalidade do plano.

**Regra de trial e inadimplência:**

- Trial de **14 dias** com cartão de crédito cadastrado; ao fim do trial o cartão é debitado automaticamente.
- Em caso de inadimplemento: **3 dias de aviso com acesso total**, seguidos de **bloqueio de escrita**.
- Durante o bloqueio de escrita, leitura e agenda continuam disponíveis por **15 dias**.
- Após 15 dias de bloqueio, o tenant é suspenso por completo.

### 0.2. Política comercial de WhatsApp e IA

O WhatsApp é funcionalidade **necessária do produto**, não opcional (diretrizes §7.1). A operação é centralizada pelo Servia e o consumo é tarifado por estabelecimento.

#### 0.2.1. Operação do WhatsApp pelo Servia

- O Servia **administra a integração e a operação** do WhatsApp, centralizando o pagamento à Meta pelo modelo **Solution Partner**.
- A operação depende de **aprovação e linha de crédito** junto à Meta. Enquanto não houver aprovação, a fase F6 não pode entrar em produção.
- O estabelecimento **não** contrata a Meta diretamente; ele consome o serviço operado pelo Servia.

#### 0.2.2. Consumo tarifado, sem subsídio

- Cada estabelecimento **paga pelo consumo tarifado que gerar**.
- **Não há subsídio**: mensagens com custo **não** são incluídas gratuitamente nos planos.
- O uso gratuito disponibilizado pela Meta é **aproveitado dentro dos planos** do Servia — ou seja, a franquia gratuita da Meta é repassada ao tenant, não retida pela plataforma.

#### 0.2.3. Formas de cobrança do WhatsApp

O estabelecimento escolhe, por configuração:

| Modalidade | Como funciona |
| --- | --- |
| **Cobrança automática** | O consumo é apurado no período e cobrado na fatura seguinte |
| **Compra antecipada de créditos** | O estabelecimento compra créditos adicionais antes de consumir |

#### 0.2.4. Controle de gastos pelo estabelecimento

- O estabelecimento define **limites de consumo** próprios, além da franquia do plano.
- O estabelecimento define **permissões por tipo de mensagem** (quais tipos podem ser enviados e por quem).
- Ao atingir o limite definido, o envio é **bloqueado no servidor**.

#### 0.2.5. Franquia de IA

- A IA tem **franquia mensal própria**, definida conforme o plano contratado.
- O consumo excedente à franquia é **cobrado adicionalmente**.
- A franquia de IA é independente da franquia de WhatsApp.

#### 0.2.6. Cobrança separada e transparência

- Os custos de **WhatsApp** e de **IA** são contabilizados e cobrados **separadamente**.
- Cada estabelecimento consegue visualizar o consumo de cada recurso e controlar seus gastos.
- A separação permite identificar a origem do custo e evita que um recurso encubra o outro na fatura.

---

## 1. Funcionalidades universais (base comum)

Recursos disponíveis para todos os tenants, independentemente do segmento.

### 1.1. Conta, autenticação e acesso

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U01 | Cadastro de estabelecimento (tenant) | Criação do tenant com nome, segmento, documento, endereço e contato do responsável | ❌ |
| U02 | Autenticação de usuários | Login com e-mail e senha, emissão de JWT | ✅ |
| U03 | Registro de usuários | Cadastro de novos usuários vinculados a um tenant | ✅ (sem tenant) |
| U04 | Recuperação de senha | Fluxo de redefinição por e-mail com token de uso único | ❌ |
| U05 | Perfil do usuário | Consulta e atualização dos próprios dados | ✅ |
| U06 | Alteração de senha | Troca de senha com confirmação da senha atual | ✅ |
| U07 | Exclusão de conta | Exclusão da própria conta com confirmação por senha | ✅ |
| U08 | Papéis e permissões | Papéis: proprietário, administrador, profissional, recepcionista, cliente | ⚠️ parcial (client/admin) |
| U08a | Identidade independente por tenant | Cada estabelecimento tem seus próprios cadastros de usuário; a mesma pessoa física possui registros distintos por tenant, sem identidade global e sem vínculo automático | ❌ |
| U09 | Convite de membros da equipe | Convite por e-mail com aceite e definição de papel | ❌ |
| U10 | Sessão e renovação de token | Refresh token com revogação e expiração controlada | ❌ |

### 1.2. Gestão de clientes

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U11 | Cadastro de clientes | Nome, contato, data de nascimento, observações; cadastro **específico do tenant** | ✅ (via `User`) |
| U12 | Histórico do cliente | Atendimentos, serviços contratados, pagamentos e agendamentos | ⚠️ parcial |
| U13 | Preferências do cliente | Profissional preferido, serviços recorrentes, notas internas | ❌ |
| U14 | Busca e segmentação | Filtros por nome, telefone, última visita e status | ⚠️ parcial |
| U15 | Consentimento LGPD | Registro de consentimento para comunicação e base legal do tratamento | ❌ |
| U16 | Anonimização e exclusão de dados | Atendimento ao direito de exclusão do titular | ❌ |

### 1.3. Serviços e catálogo

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U17 | Cadastro de serviços | Nome, descrição, preço, duração, destaque | ✅ |
| U18 | Ativação e desativação de serviços | Soft delete preservando histórico | ✅ |
| U19 | Categorias de serviços | Agrupamento para exibição no site e no agendamento | ❌ |
| U20 | Variações de serviço | Duração e preço variáveis por profissional ou por tipo de cabelo/área | ❌ |
| U21 | Combos de serviços | Pacote de serviços executados na mesma sessão | ❌ |

### 1.4. Profissionais e equipe

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U22 | Cadastro de profissionais | Nome, função, bio, foto, serviços que executa | ✅ (básico) |
| U23 | Jornada de trabalho | Dias da semana e horário de início/fim | ✅ |
| U24 | Bloqueios e folgas | Bloqueio de datas e horários específicos | ❌ |
| U25 | Vínculo profissional ↔ serviço | Quais serviços cada profissional executa | ❌ |
| U26 | Agenda por profissional | Visualização individual da agenda | ⚠️ parcial |

### 1.5. Agendamento e disponibilidade

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U27 | Consulta de disponibilidade | Geração de horários livres por serviço, profissional e data | ✅ |
| U28 | Criação de agendamento | Reserva de horário com validação de conflito | ✅ |
| U28a | Reserva sob concorrência | Duas solicitações simultâneas para o mesmo profissional e horário resultam em **uma única** reserva confirmada; a outra é rejeitada | ❌ |
| U29 | Remarcação | Alteração de data/hora preservando o histórico | ✅ |
| U30 | Cancelamento | Cancelamento com registro de motivo | ✅ |
| U31 | Conclusão de atendimento | Marcação de atendimento realizado | ✅ |
| U32 | Listagem de agendamentos | Próprios agendamentos (cliente) e agenda completa (equipe) | ✅ |
| U33 | Regras de antecedência | Tempo mínimo para agendar e para cancelar | ⚠️ parcial (lead time fixo) |
| U34 | Fila de espera | Lista de espera com oferta automática de vagas liberadas | ❌ |
| U35 | Sinal para reserva | Exigência de pagamento antecipado para confirmar | ❌ |
| U36 | Sessões prolongadas | Suporte a serviços com duração longa e múltiplos blocos | ❌ |

### 1.6. Site e presença digital

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U37 | Site individual por tenant | Página pública com identidade visual do estabelecimento | ⚠️ (landing estática) |
| U38 | Subdomínio da plataforma | Endereço `{slug}.plataforma.com` | ❌ |
| U39 | Domínio próprio | Vinculação de domínio próprio com SSL automatizado | ❌ |
| U40 | Editor de conteúdo | Edição de textos, imagens, seções e ordem | ❌ |
| U41 | Prévia multi-dispositivo | Visualização em desktop, tablet e mobile | ❌ |
| U42 | Publicação com aprovação | Alterações só entram no ar após aprovação explícita | ❌ |
| U43 | Agendamento embutido no site | Widget de agendamento integrado à agenda real | ⚠️ parcial |
| U44 | Galeria de trabalhos | Portfólio de fotos por profissional ou por serviço | ⚠️ (estática) |

### 1.7. Comunicação e notificações

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U45 | Confirmação de agendamento | Envio automático de confirmação | ❌ |
| U46 | Lembretes | Lembrete antes do horário agendado | ❌ |
| U47 | Avisos de alteração | Comunicação de remarcação e cancelamento | ❌ |
| U48 | Canais de notificação | E-mail e WhatsApp; SMS como opcional | ❌ |
| U48a | Controle de consumo do WhatsApp | Consumo medido por tenant e por período; envio bloqueado no servidor ao atingir o limite | ❌ |
| U48b | Compra antecipada de créditos de WhatsApp | Estabelecimento compra créditos adicionais antes de consumir | ❌ |
| U48c | Cobrança automática do consumo de WhatsApp | Consumo apurado no período e cobrado na fatura seguinte | ❌ |
| U48d | Limites de consumo definidos pelo estabelecimento | Tenant define teto próprio de consumo, além da franquia do plano | ❌ |
| U48e | Permissões por tipo de mensagem | Tenant define quais tipos de mensagem podem ser enviados e por quem | ❌ |
| U48f | Aproveitamento do uso gratuito da Meta | Franquia gratuita da Meta é repassada ao tenant dentro do plano | ❌ |
| U49 | Templates de mensagem | Modelos editáveis por tenant com variáveis | ❌ |
| U50 | Preferências de comunicação | Controle do cliente sobre quais avisos receber | ❌ |

### 1.8. Pagamentos e financeiro

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U51 | Registro de pagamento | Registro de pagamento presencial ou por outro meio | ❌ |
| U52 | Pagamento online | Cobrança no momento do agendamento | ❌ |
| U53 | Formas de pagamento | Dinheiro, cartão, Pix, transferência | ❌ |
| U54 | Receitas e despesas | Lançamentos financeiros com categorias | ❌ |
| U55 | Relatórios financeiros | Faturamento, ticket médio, recebíveis por período | ❌ |
| U56 | Estornos e cancelamentos financeiros | Tratamento de estorno e ajuste de lançamento | ❌ |
| U56a | Conciliação financeira | Conferência entre pagamentos registrados, transações do gateway e lançamentos internos | ❌ |

### 1.9. Administração da plataforma

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U57 | Painel administrativo da plataforma | Gestão de tenants, planos e status de assinatura | ❌ |
| U57a | Separação de privilégios da plataforma | Privilégios administrativos da plataforma são distintos das permissões de usuários de tenant, com controle explícito das operações administrativas | ❌ |
| U57b | Operação do WhatsApp como Solution Partner | Servia centraliza a integração e o pagamento à Meta, com controle de aprovação e linha de crédito | ❌ |
| U57c | Franquia mensal de IA por plano | Franquia definida pelo plano, com cobrança adicional pelo excedente | ❌ |
| U57d | Cobrança separada de WhatsApp e IA | Custos contabilizados e cobrados em linhas distintas, com visão de consumo por recurso | ❌ |
| U58 | Auditoria | Registro de ações sensíveis com autor, data e origem | ❌ |
| U59 | Observabilidade | Logs estruturados, métricas e rastreamento de requisições | ❌ |
| U60 | Backup e restauração | Política de backup por tenant e restauração testada | ❌ |

---

## 2. Módulos opcionais (habilitados por plano ou por tenant)

### 2.1. Assinaturas, clubes e pacotes

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| M01 | Clube de assinatura | Planos mensais com benefícios definidos | ❌ |
| M02 | Pacotes de serviços | Quantidade pré-paga de sessões com validade | ❌ |
| M03 | Controle de benefícios | Consumo, saldo e validade por cliente | ❌ |
| M04 | Cobrança recorrente | Renovação automática com retentativa | ❌ |
| M05 | Gestão de inadimplência | Suspensão de benefício e régua de cobrança | ❌ |

### 2.2. Comissões e remuneração

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| M06 | Regras de comissão | Percentual por serviço, por profissional ou por meta | ❌ |
| M07 | Cálculo de comissões | Apuração por período com base em atendimentos concluídos | ❌ |
| M08 | Extrato do profissional | Visualização da própria remuneração | ❌ |

### 2.3. Automação de atendimento (WhatsApp)

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| M09 | Atendimento automatizado | Respostas a dúvidas frequentes e apresentação de serviços | ❌ |
| M10 | Agendamento por conversa | Consulta de disponibilidade e criação de agendamento pelo WhatsApp | ❌ |
| M11 | Atendimento fora do horário | Continuidade do atendimento sem intervenção humana | ❌ |
| M12 | Transferência para humano | Escalonamento da conversa para a equipe | ❌ |
| M13 | Histórico de conversas | Registro vinculado ao cliente | ❌ |

### 2.4. Relacionamento e fidelização

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| M14 | Programa de fidelidade | Pontos ou selos por atendimento | ❌ |
| M15 | Recuperação de clientes inativos | Campanhas para clientes sem visita recente | ❌ |
| M16 | Campanhas de comunicação | Disparos segmentados com controle de consentimento | ❌ |
| M17 | Avaliações e NPS | Coleta de avaliação após o atendimento | ❌ |

### 2.5. Geração de site por IA

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| M18 | Geração inicial do site | IA planeja estrutura, seções e identidade visual a partir dos dados do tenant | ❌ |
| M19 | Geração por referências | IA analisa sites e imagens de referência fornecidos | ❌ |
| M20 | Personalização conversacional | Alterações por linguagem natural com nova prévia | ❌ |
| M21 | Guardrails de conteúdo | IA não inventa preços, serviços, qualificações ou avaliações | ❌ |
| M22 | Registro de operações da IA | Histórico de prompts, alterações e aprovações | ❌ |

---

## 3. Configurações e adaptações por segmento

Ajustes que permitem atender diferentes atividades sem criar aplicações independentes.

**Princípio (diretrizes §2):** o núcleo é universal e **não** contém condicionais que identifiquem o segmento. As particularidades são implementadas por **módulos, políticas de negócio e comportamentos especializados**, acessados por interfaces e polimorfismo. Uma configuração nova só entra se puder ser reutilizada por mais de um segmento.

| ID | Configuração | Segmentos impactados | Existe hoje |
| --- | --- | --- | --- |
| S01 | Segmento do tenant | Todos | ❌ |
| S02 | Duração padrão e granularidade de horários | Todos | ⚠️ parcial (30 min fixo) |
| S03 | Regras de sinal e no-show | Tatuagem, estética, clínicas | ❌ |
| S04 | Sessões prolongadas e múltiplos blocos | Tatuagem, massoterapia, tranças | ❌ |
| S05 | Histórico de procedimentos e fichas | Estética, clínicas, sobrancelhas | ❌ |
| S06 | Pacotes de manutenção | Barbearia, salão, unhas | ❌ |
| S07 | Comissões e combos | Barbearia, salão | ❌ |
| S08 | Múltiplos profissionais por atendimento | Salão, estética | ❌ |
| S09 | Recursos por profissional (sala, maca, cadeira) | Clínicas, estética | ❌ |
| S10 | Regras de utilização de benefícios | Clubes e pacotes | ❌ |
| S11 | Módulos habilitados por tenant | Todos | ❌ |
| S12 | Identidade visual e tema do site | Todos | ❌ |
| S13 | Política de agendamento por segmento | Todos | ❌ |
| S14 | Política de disponibilidade por segmento | Todos | ❌ |

### 3.1. Políticas de negócio por segmento

As particularidades são resolvidas por **políticas** injetadas no núcleo, não por condicionais. O núcleo conhece apenas o contrato; a implementação é escolhida pela configuração do tenant.

| Política | Contrato | Variações previstas |
| --- | --- | --- |
| Política de agendamento | Valida antecedência, sinal, no-show e cancelamento | Padrão, com sinal, com sessão prolongada |
| Política de disponibilidade | Calcula slots a partir da jornada, bloqueios e duração | Granularidade fixa, duração variável, múltiplos blocos |
| Política de comissão | Calcula remuneração do profissional | Percentual, valor fixo, por meta |
| Política de benefícios | Consome clube e pacote | Por período, por sessão, por serviço |

**Regra de extensão:** um novo segmento é atendido por **configuração** ou por uma **nova implementação de política** — nunca por um `if` de segmento no núcleo.

---

## 4. Requisitos não funcionais

| ID | Requisito | Descrição |
| --- | --- | --- |
| N01 | Isolamento de dados | Toda consulta filtrada por `TenantId`; teste automatizado que falha se o filtro for removido |
| N01a | Isolamento em todas as camadas | Resolução confiável do tenant no servidor, validação de pertencimento do usuário, verificação de `TenantId` na escrita e restrições de integridade no banco |
| N01b | Testes de isolamento | Testes que tentam consultar, alterar e excluir dados de outro tenant e comprovam a rejeição |
| N01c | Concorrência no agendamento | Reservas simultâneas do mesmo horário resultam em uma única confirmação, garantida no banco |
| N02 | Segurança | Senhas com hash, JWT com refresh revogável, HTTPS obrigatório, proteção CSRF |
| N03 | Autorização | Permissões verificadas no servidor por papel e por recurso |
| N04 | LGPD | Consentimento registrado, exclusão e anonimização, retenção definida |
| N05 | Auditoria | Ações sensíveis registradas de forma imutável |
| N06 | Desempenho | Consulta de disponibilidade abaixo de 300 ms no p95 |
| N07 | Disponibilidade | Meta de 99,5% com monitoramento e alertas |
| N08 | Responsividade | Site e painel utilizáveis em desktop, tablet e mobile |
| N09 | Acessibilidade | Conformidade com WCAG 2.1 AA nas páginas públicas |
| N10 | Escalabilidade | Suportar milhares de tenants sem alteração de arquitetura |
| N11 | Observabilidade | Logs estruturados, métricas e trace de requisição |
| N12 | Testes | Regras de domínio, casos de uso e isolamento multi-tenant cobertos |
| N13 | Testes de integração externa | Integrações testadas com falha, indisponibilidade, repetição e resposta inesperada |
| N14 | Núcleo sem condicionais de segmento | O núcleo não identifica segmento por condicional; particularidades entram por módulos e políticas |

---

## 5. Referência ao código acadêmico

O código acadêmico **não** é base do Servia (ADR-001) e foi removido do repositório em 2026-11-10. O registro do que existia está em `docs/levantamento_lacunas.md`.

O que o código acadêmico demonstra é o **domínio mínimo** que o Servia precisa cobrir — e que será implementado de forma nova, com as regras, o modelo e a arquitetura definidos em `docs/`:

| Área | O que o código acadêmico demonstra | Como o Servia trata |
| --- | --- | --- |
| Autenticação e usuários | Login, cadastro, perfil, troca de senha | Implementado de novo, com identidade por tenant (ADR-015) |
| Catálogo de serviços | Cadastro, preço, duração, ativação | Implementado de novo, com categorias, variações e combos |
| Profissionais e jornada | Profissional com jornada semanal | Implementado de novo como `Professional` + `WorkSchedule` (ADR-010) |
| Agendamento | Disponibilidade, criação, remarcação, cancelamento, conclusão | Implementado de novo, com políticas injetadas e controle de concorrência (ADR-017, ADR-018) |
| Site e agendamento público | Landing e wizard de agendamento | Implementado de novo como site por tenant |

> **Regra:** nenhuma linha de código acadêmico é copiada para o Servia. O domínio é redesenhado a partir de `docs/classes.md` e das diretrizes em `docs/diretrizes.md`.

---

## 6. Separação dos contextos financeiros

Conforme diretrizes §8, existem **dois domínios financeiros independentes**, com responsabilidades, regras e registros próprios. Nenhum deles referencia o outro em suas regras de negócio.

| Contexto | Quem é o titular | O que cobre | Funcionalidades |
| --- | --- | --- | --- |
| **Financeiro do estabelecimento** | O tenant | Gestão das finanças do próprio negócio | U51–U56a, M01–M08 |
| **Billing da plataforma** | O Servia | Relação comercial com os tenants | U57–U57d, F4 do Roadmap |

**Regras de independência:**

- O financeiro do tenant **não** enxerga faturas da plataforma, e o billing **não** enxerga receitas do estabelecimento.
- Cada contexto tem seus próprios agregados, tabelas e relatórios.
- Ambos podem usar o mesmo gateway (`IPaymentGateway`), mas com contratos e registros separados.
- Uma falha em um contexto não corrompe o outro.

### 6.1. Linhas de cobrança do billing

O billing da plataforma emite **linhas de cobrança distintas** por origem, garantindo transparência:

| Linha | Origem | Base de cálculo |
| --- | --- | --- |
| Assinatura | Plano contratado | Mensalidade fixa |
| WhatsApp | Consumo tarifado | Mensagens acima da franquia gratuita da Meta, ou créditos antecipados |
| IA | Consumo acima da franquia | Uso excedente à franquia mensal do plano |
| Alterações de site | `SiteChangeCharge` | Rodadas de alteração após a publicação |

Cada linha é rastreável até o consumo que a originou, e o tenant visualiza o detalhamento por recurso.

---

## 6A. Lacunas para operação plena (2026-11-11)

O roadmap F0–F15 foi concluído: domínio, API, integrações e frontend existem e são testados. Um levantamento do estado real, porém, identificou **quatro blocos de lacunas** que impedem o sistema de ser operado por um cliente sem intervenção técnica.

**Critério de "100% funcional e operante":** um estabelecimento consegue se cadastrar, configurar o site, escolher e trocar de plano, e o sistema executa sozinho as rotinas de notificação e cobrança — tudo pela interface, sem acesso ao banco ou à API por fora.

### 6A.1. Billing sem escrita (bloco F16)

O `BillingController` tem **4 endpoints, todos `GET`**. Não existe forma de escolher, trocar ou cancelar um plano após o provisionamento.

| Lacuna | Impacto |
| --- | --- |
| Sem `POST /billing/subscription` | O plano só é definido no provisionamento; não há upgrade nem downgrade |
| Sem `POST /billing/subscription/cancel` | Cliente não consegue encerrar a assinatura |
| Sem regra de upgrade por faixa | A regra "ao ultrapassar o limite de profissionais, sobe de categoria" (U57) não é aplicada |
| Sem histórico de mudanças de plano | Não há rastreabilidade de quando o tenant mudou de categoria |

### 6A.2. Webhooks ausentes (bloco F17)

As integrações foram implementadas como **clientes** (o Servia chama o provedor), mas não como **receptores**. Sem webhook, o Servia nunca sabe que um pagamento foi aprovado ou que uma mensagem chegou.

| Lacuna | Impacto |
| --- | --- |
| Sem `POST /webhooks/mercadopago` | Pagamento aprovado não confirma a fatura; assinatura não ativa |
| Sem `POST /webhooks/whatsapp` | Mensagem recebida do cliente não entra na conversa; bot não responde |
| Sem validação de assinatura do webhook | Qualquer origem poderia forjar um evento |
| Sem idempotência persistida | `IIdempotencyStore` é em memória; reinício reprocessa eventos |

### 6A.3. Frontend somente leitura (bloco F18)

As **7 páginas do painel** fazem apenas `GET`. A única página com escrita é o login.

| Página | Estado atual | Necessário |
| --- | --- | --- |
| `/painel` — Agenda | Lista por data | Criar, remarcar, cancelar, concluir |
| `/painel/servicos` | Lista | Criar, alterar preço, desativar |
| `/painel/equipe` | Lista | Criar profissional, vincular serviço, bloquear agenda |
| `/painel/clientes` | Lista com busca | Cadastrar, editar, anonimizar (LGPD) |
| `/painel/financeiro` | Relatório do mês | Registrar pagamento, lançar receita/despesa |
| `/painel/site` | Exibe estado | Criar site, editar seções, publicar, rollback |
| `/painel/assinatura` | Exibe plano | Escolher plano, trocar, cancelar |

Além disso, falta a **tela de provisionamento** para o administrador da plataforma — a API existe (`POST /platform/tenants`), a interface não.

### 6A.4. Nenhum job em background (bloco F19)

Não há `BackgroundService` nem `IHostedService`. Toda rotina que depende de tempo nunca executa.

| Rotina | Entidade existe | Executa sozinha |
| --- | --- | --- |
| Enviar lembrete de agendamento | `Notification` com `ScheduledAt` | ❌ |
| Enviar confirmação após criação | `Notification` | ❌ |
| Cobrar assinatura no vencimento | `PlatformInvoice` com `DueDate` | ❌ |
| Aplicar régua de inadimplência | `DelinquencyPolicy` | ❌ |
| Expirar `ScheduleLock` | `ScheduleLock.ExpiresAt` | ❌ |
| Renovar período de clube | `Membership.Renew` | ❌ |
| Disparar campanha de inativos | `ReactivationCampaign` | ❌ |
| Aplicar retenção de dados | `RetentionPolicy` | ❌ |

### 6A.5. Lacuna de bootstrap

Não existe endpoint nem comando para criar o **primeiro administrador da plataforma**. Hoje é necessário inserir no banco com hash PBKDF2 calculado à mão.

### 6A.6. Dívida arquitetural (não bloqueia operação)

**9 dos 13 controllers** acessam `AppDbContext` diretamente. A lógica de negócio vive nos controllers, não na camada de aplicação — que tem apenas 3 serviços (`AppointmentService`, `AuthenticationService`, `TenantProvisioner`).

Isso não impede a operação, mas viola a separação descrita no `ARCH.md` e dificulta testar a lógica sem subir a API. Registrado como recomendação, não como bloqueio.

---

## 7. Pendências e dependências externas

### 7.1. Pendências abertas

Itens que dependem de decisão do responsável do produto e não bloqueiam o planejamento.

| # | Pendência | Origem | Quando resolver |
| --- | --- | --- | --- |
| PD2 | Provedor de LLM e teto de custo por geração | decisão D6 | Fase F8 |
| PD3 | Valores das franquias de WhatsApp e de IA por plano | política comercial §0.2 | Antes da fase F4 |
| PD4 | Tarifa de repasse do consumo de WhatsApp ao tenant | política comercial §0.2.2 | Antes da fase F6 |

> **PD1 encerrada em 2026-11-10:** a política comercial de consumo do WhatsApp foi definida na seção 0.2.

### 7.2. Dependências externas

| # | Dependência | Impacto se não atendida |
| --- | --- | --- |
| DE1 | Aprovação do Servia como **Solution Partner** da Meta | Fase F6 não entra em produção |
| DE2 | **Linha de crédito** junto à Meta para centralizar o pagamento | Consumo de WhatsApp inviável em escala |

---

## 8. Fora de escopo da v1

Itens da visão de produto que ficam explicitamente para depois do MVP:

- Aplicativo móvel nativo (iOS/Android).
- Marketplace público de estabelecimentos.
- Integrações com ERPs e sistemas fiscais.
- Emissão de nota fiscal.
- Multi-idioma e multi-moeda.
- Programa de indicação entre tenants.
