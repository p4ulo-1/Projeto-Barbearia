# Funcionalidades — Plataforma SaaS de Gestão e Atendimento para Negócios de Serviços

> **Status:** v1 aprovada — as 12 decisões estruturais foram confirmadas pelo responsável do produto em 2026-11-04.
> **Fonte:** `docs/descricao_produto.md` (visão de produto) + código existente em `backend/` e `frontend/`.
> **Registro das decisões:** ver seção 0 e `docs/gerenciamento/ADR.md`.

---

## 0. Premissas estruturais adotadas

| # | Decisão | Escolha adotada | Status |
| --- | --- | --- | --- |
| P1 | Relação com o código atual | O sistema da barbearia vira o **primeiro tenant** da plataforma; backend e frontend são reaproveitados | ✅ confirmado |
| P2 | Multi-tenancy | **Banco único** com `TenantId` em todas as entidades de negócio + filtro global no EF Core | ✅ confirmado |
| P3 | Banco de produção | **PostgreSQL**; SQLite apenas em testes locais | ✅ confirmado |
| P4 | Escopo do MVP | **Escopo completo da descrição de produto** (fases F0–F9) | ✅ confirmado |
| P5 | Gateways de pagamento | **Somente Mercado Pago** (Pix, boleto e cartão), atrás da abstração `IPaymentGateway` | ✅ confirmado |
| P6 | WhatsApp | **Cloud API oficial da Meta**, com camada de abstração para trocar de provedor | ✅ confirmado |
| P7 | IA de site | LLM via API + biblioteca de componentes controlada pela plataforma; **provedor a definir na fase F8** | ✅ confirmado |
| P8 | Infra | Docker + GitHub Actions + PaaS na primeira fase | ✅ confirmado |
| P9 | LGPD | Cada estabelecimento é **controlador** dos dados dos seus clientes finais; a plataforma é **operadora** | ✅ confirmado |

### 0.1. Modelo de planos

**Todos os planos oferecem todos os serviços e módulos.** A diferença entre planos está na quantidade de profissionais incluídos e no grau de personalização do site.

| Plano | Profissionais incluídos | Módulos | Personalização do site | Observação |
| --- | --- | --- | --- | --- |
| Essencial | até 3 | Todos | Básica — ajustes limitados de tema e conteúdo | Entrada |
| Pro | até 7 | Todos | Intermediária — mais seções e variações visuais | Mais vendido |
| Premium | até 15 | Todos | Avançada — ampla liberdade de layout e identidade | Completo |
| Business | ilimitado | Todos | Total — máxima liberdade de personalização | Grandes operações |

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
| U09 | Convite de membros da equipe | Convite por e-mail com aceite e definição de papel | ❌ |
| U10 | Sessão e renovação de token | Refresh token com revogação e expiração controlada | ❌ |

### 1.2. Gestão de clientes

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U11 | Cadastro de clientes | Nome, contato, data de nascimento, observações | ✅ (via `User`) |
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

### 1.9. Administração da plataforma

| ID | Funcionalidade | Descrição | Existe hoje |
| --- | --- | --- | --- |
| U57 | Painel administrativo da plataforma | Gestão de tenants, planos e status de assinatura | ❌ |
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

---

## 4. Requisitos não funcionais

| ID | Requisito | Descrição |
| --- | --- | --- |
| N01 | Isolamento de dados | Toda consulta filtrada por `TenantId`; teste automatizado que falha se o filtro for removido |
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

---

## 5. Mapa de rastreabilidade — funcionalidades existentes

O que já está implementado no código atual e será preservado na migração para multi-tenant.

| Funcionalidade | Onde está hoje | Ação na migração |
| --- | --- | --- |
| U02, U03, U05, U06, U07 | `AuthService`, `UserService`, `AuthController`, `UsersController` | Adicionar `TenantId` e refresh token |
| U17, U18 | `ServiceService`, `ServicesController` | Adicionar categorias e variações |
| U22, U23 | `Barber`, `BarberSchedule`, `BarberService` | Adicionar vínculo com serviços e bloqueios |
| U27–U32 | `AppointmentService`, `AppointmentsController` | Tornar regras configuráveis por tenant |
| U37, U43, U44 | Rotas `index` e `agendamento` do frontend | Converter em site por tenant |
| U08 | Papéis `client`/`admin` | Expandir para papéis por tenant |

---

## 6. Fora de escopo da v1

Itens da visão de produto que ficam explicitamente para depois do MVP:

- Aplicativo móvel nativo (iOS/Android).
- Marketplace público de estabelecimentos.
- Integrações com ERPs e sistemas fiscais.
- Emissão de nota fiscal.
- Multi-idioma e multi-moeda.
- Programa de indicação entre tenants.
