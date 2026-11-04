# Roadmap — Plataforma SaaS de Gestão e Atendimento

> **Status:** v1 aprovada em 2026-11-04. Decisões estruturais confirmadas pelo responsável do produto.
> **Escopo do MVP:** completo — todas as fases F0–F9 fazem parte da primeira versão.
> **Unidade de planejamento:** fases com entregas verificáveis. Prazos em semanas relativas ao início da fase.

---

## Visão geral das fases

| Fase | Nome | Objetivo | Duração estimada | Depende de |
| --- | --- | --- | --- | --- |
| F0 | Fundação técnica | Preparar infra, CI e base de testes para o SaaS | 2 semanas | — |
| F1 | Multi-tenancy | Isolar dados por tenant e migrar o sistema atual | 4 semanas | F0 |
| F2 | Núcleo universal | Completar cadastros, equipe, agenda e clientes | 5 semanas | F1 |
| F3 | Site por tenant | Site individual, subdomínio e publicação | 4 semanas | F2 |
| F4 | Billing da plataforma | Planos, assinatura, gateway e inadimplência | 4 semanas | F1 |
| F5 | Financeiro do tenant | Pagamentos, receitas, despesas e comissões | 4 semanas | F2 |
| F6 | Comunicação e WhatsApp | Notificações e automação de atendimento | 5 semanas | F2 |
| F7 | Módulos opcionais | Clubes, pacotes e fidelização | 4 semanas | F5 |
| F8 | IA de site | Geração e personalização conversacional | 5 semanas | F3 |
| F9 | Escala e operação | Observabilidade, LGPD e hardening | 3 semanas | F4–F8 |

**MVP** = F0 + F1 + F2 + F3 + F4 + F5 + F6 + F7 + F8 + F9. Total estimado: **40 semanas**.

> **Nota:** o escopo do MVP foi ampliado por decisão do responsável do produto em 2026-11-04. A primeira versão entrega o produto completo descrito em `docs/descricao_produto.md`, incluindo WhatsApp, IA de site e módulos opcionais. As fases F5–F9 deixam de ser pós-MVP.

---

## F0 — Fundação técnica (2 semanas)

**Objetivo:** criar a base de infraestrutura e qualidade sobre a qual o SaaS será construído.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F0.1 | Dockerfile do backend e do frontend | Build reproduzível em ambiente limpo | N11 |
| F0.2 | `docker-compose` com API, frontend e PostgreSQL | `docker compose up` sobe o ambiente completo | N11 |
| F0.3 | Pipeline GitHub Actions (build + testes) | Pipeline verde em PR para `main` | N12 |
| F0.4 | Interfaces de repositório e `IUnitOfWork` | Repositórios concretos implementam contratos; testes existentes continuam passando | N12 |
| F0.5 | `IClock` e injeção de dependência de tempo | Testes de agendamento determinísticos | N12 |
| F0.6 | Logs estruturados e health check | `/health` responde estado de API e banco | U59, N11 |
| F0.7 | Migração de SQLite para PostgreSQL | Migrations aplicadas em Postgres; testes rodam em container | N10 |

### Riscos
- Migração de banco pode revelar incompatibilidades de tipos (`DateOnly`, `TimeOnly`, índices `NOCASE`).
- Mitigação: migration de teste em ambiente isolado antes de tocar dados reais.

---

## F1 — Multi-tenancy (4 semanas)

**Objetivo:** transformar o sistema single-shop em plataforma multi-tenant, preservando o código existente.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F1.1 | Entidades `Tenant`, `Plan`, `Subscription` | CRUD administrativo funcional | U01 |
| F1.2 | `TenantId` em todas as entidades de negócio | Migration aplicada sem perda de dados | N01 |
| F1.3 | Filtro global no EF Core + `ITenantContext` | Teste automatizado falha se o filtro for removido | N01 |
| F1.4 | Migração do sistema atual como primeiro tenant | Dados existentes acessíveis no novo modelo | U01 |
| F1.5 | Papéis por tenant (`Owner`, `Administrator`, `Professional`, `Receptionist`, `Customer`) | Autorização verificada no servidor | U08, N03 |
| F1.6 | Refresh token com revogação | Sessão renova e revoga corretamente | U10, N02 |
| F1.7 | Convite de membros da equipe | Convite por e-mail com aceite e papel definido | U09 |
| F1.8 | Auditoria de ações sensíveis | `AuditEntry` registra autor, ação e origem | U58, N05 |

### Riscos
- Vazamento de dados entre tenants é o risco crítico. Mitigação: testes de isolamento obrigatórios no pipeline.

---

## F2 — Núcleo universal (5 semanas)

**Objetivo:** completar as funcionalidades universais da base comum.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F2.1 | `Customer` separado de `User` | Cliente sem login cadastrado e vinculado a agendamentos | U11 |
| F2.2 | Histórico e preferências do cliente | Histórico consultável por cliente | U12, U13 |
| F2.3 | Categorias, variações e combos de serviço | Catálogo configurável por tenant | U19, U20, U21 |
| F2.4 | `Professional` generalizado (substitui `Barber`) | Profissionais de qualquer segmento cadastrados | U22 |
| F2.5 | Vínculo profissional ↔ serviço | Disponibilidade respeita o vínculo | U25 |
| F2.6 | Bloqueios e folgas de agenda | Horário bloqueado não aparece como disponível | U24 |
| F2.7 | Regras de antecedência configuráveis | Lead time por tenant | U33 |
| F2.8 | Fila de espera | Vaga liberada ofertada ao primeiro da fila | U34 |
| F2.9 | Sinal para reserva | Agendamento exige depósito quando configurado | U35 |
| F2.10 | Sessões prolongadas | Serviço com duração longa ocupa múltiplos blocos | U36 |

### Riscos
- Regras de antecedência e sinal alteram o fluxo de agendamento existente. Mitigação: manter testes de regressão do fluxo atual.

---

## F3 — Site por tenant (4 semanas)

**Objetivo:** entregar site individual com publicação controlada.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F3.1 | `Site`, `SitePage`, `SiteSection`, `SiteTheme` | Site criado por tenant com páginas e seções | U37 |
| F3.2 | Biblioteca de componentes visuais | Componentes reutilizáveis com configuração validada | U40 |
| F3.3 | Subdomínio `{slug}.plataforma.com` | Resolução de tenant por subdomínio | U38 |
| F3.4 | Editor de conteúdo | Textos, imagens, seções e ordem editáveis | U40 |
| F3.5 | Prévia multi-dispositivo | Visualização em desktop, tablet e mobile | U41 |
| F3.6 | Publicação com aprovação | Alteração só entra no ar após aprovação explícita | U42 |
| F3.7 | Rollback de versão publicada | Restauração de versão anterior | U42 |
| F3.8 | Agendamento embutido no site | Widget consome a agenda real do tenant | U43 |

### Riscos
- Domínio próprio com SSL fica para depois do MVP. Mitigação: subdomínio atende o lançamento.

---

## F4 — Billing da plataforma (4 semanas)

**Objetivo:** monetizar o SaaS com assinatura e cobrança automatizada.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F4.1 | `IPaymentGateway` com implementação Mercado Pago | Cobrança e assinatura criadas no gateway (Pix, boleto, cartão) | U52 |
| F4.2 | Cadastro de cartão no trial | Cartão de crédito registrado no início do trial de 14 dias | U52 |
| F4.3 | `PlatformInvoice` e ciclo de faturamento | Fatura gerada, paga e vencida corretamente | U57 |
| F4.4 | Webhooks de pagamento | Evento do gateway atualiza status da assinatura | U52 |
| F4.5 | Trial de 14 dias com débito automático | Cartão debitado ao fim do trial sem intervenção manual | U57 |
| F4.6 | Régua de inadimplência | 3 dias de aviso com acesso total, depois bloqueio de escrita | U57 |
| F4.7 | Bloqueio de escrita com leitura por 15 dias | Leitura e agenda disponíveis durante o bloqueio; suspensão total após 15 dias | U57 |
| F4.8 | Painel administrativo da plataforma | Gestão de tenants, planos e assinaturas | U57 |
| F4.9 | Todos os módulos liberados em todos os planos | Nenhum módulo bloqueado por plano; verificação no servidor | S11 |
| F4.10 | Regra de upgrade por excedente de profissionais | Tenant que ultrapassa o limite do plano é obrigado a subir de categoria | U57 |
| F4.11 | Limite de personalização do site por plano | Plano contratado define o grau de personalização disponível | S11 |

### Planos confirmados

**Todos os planos oferecem todos os serviços e módulos.** A diferenciação ocorre por quantidade de profissionais e grau de personalização do site.

| Plano | Profissionais incluídos | Módulos | Personalização do site |
| --- | --- | --- | --- |
| Essencial | até 3 | Todos | Básica |
| Pro | até 7 | Todos | Intermediária |
| Premium | até 15 | Todos | Avançada |
| Business | ilimitado | Todos | Total |

Sem taxa por profissional excedente: ao ultrapassar o limite, o tenant sobe de categoria.

**Cobrança de alterações do site:** a geração inicial e a publicação estão incluídas no plano. Alterações posteriores ao lançamento são cobradas à parte, por rodada de alteração.

### Riscos
- Webhooks duplicados ou fora de ordem. Mitigação: idempotência por `GatewayPaymentId`.
- Falha de cartão no fim do trial gerando churn involuntário. Mitigação: régua de aviso de 3 dias antes do bloqueio.

---

## F5 — Financeiro do tenant (4 semanas)

**Objetivo:** dar ao estabelecimento controle financeiro completo.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F5.1 | Registro de pagamentos presenciais | Pagamento registrado por forma e atendimento | U51, U53 |
| F5.2 | Pagamento online no agendamento | Cobrança criada e confirmada por webhook | U52 |
| F5.3 | Receitas e despesas com categorias | Lançamentos consultáveis por período | U54 |
| F5.4 | Relatórios financeiros | Faturamento, ticket médio e recebíveis por período | U55 |
| F5.5 | Estornos e ajustes | Estorno refletido no relatório | U56 |
| F5.6 | Regras de comissão | Comissão calculada por serviço, profissional ou meta | M06, M07 |
| F5.7 | Extrato do profissional | Profissional consulta a própria remuneração | M08 |

---

## F6 — Comunicação e WhatsApp (5 semanas)

**Objetivo:** automatizar atendimento e comunicação.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F6.1 | `IMessagingProvider` com Cloud API da Meta | Mensagem enviada e recebida em ambiente de teste | U48 |
| F6.2 | Templates de mensagem por tenant | Template editável com variáveis | U49 |
| F6.3 | Confirmação e lembretes | Notificação disparada nos horários configurados | U45, U46 |
| F6.4 | Avisos de alteração | Remarcação e cancelamento comunicados | U47 |
| F6.5 | Atendimento automatizado | Dúvidas frequentes respondidas sem intervenção | M09, M11 |
| F6.6 | Agendamento por conversa | Agendamento criado pelo WhatsApp | M10 |
| F6.7 | Transferência para humano | Conversa escalada para a equipe | M12 |
| F6.8 | Histórico de conversas | Conversa vinculada ao cliente | M13 |

### Riscos
- Aprovação de templates pela Meta e limites de mensagens. Mitigação: iniciar o processo de verificação do número na F0.

---

## F7 — Módulos opcionais (4 semanas)

**Objetivo:** habilitar modelos comerciais recorrentes e pré-pagos.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F7.1 | Clubes de assinatura | Plano criado com benefícios e cobrança recorrente | M01, M04 |
| F7.2 | Pacotes de serviços | Pacote vendido com saldo e validade | M02 |
| F7.3 | Controle de benefícios | Consumo e saldo atualizados por atendimento | M03 |
| F7.4 | Gestão de inadimplência do clube | Benefício suspenso conforme régua | M05 |
| F7.5 | Programa de fidelidade | Pontos creditados por atendimento concluído | M14 |
| F7.6 | Recuperação de clientes inativos | Campanha disparada para clientes sem visita recente | M15, M16 |
| F7.7 | Avaliações e NPS | Avaliação coletada após atendimento concluído | M17 |

---

## F8 — IA de site (5 semanas)

**Objetivo:** entregar geração e personalização de site por IA com guardrails.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F8.1 | `IAiSiteGenerator` com provedor de LLM | Geração inicial produz site válido | M18 |
| F8.2 | Geração por referências | Referências influenciam o resultado | M19 |
| F8.3 | Personalização conversacional | Alteração por linguagem natural gera nova prévia | M20 |
| F8.4 | Guardrails de conteúdo | IA não inventa preço, serviço, qualificação ou avaliação | M21 |
| F8.5 | `AiChangeSet` com aprovação | Nada publicado sem aprovação explícita | M22 |
| F8.6 | Registro de operações e custo | Tokens e custo registrados por requisição | M22 |
| F8.7 | Limite de custo por tenant | Requisição bloqueada ao exceder o teto | M22 |
| F8.8 | Limite de personalização por plano | Plano Essencial permite ajustes básicos; Business permite personalização total | M20 |
| F8.9 | Cobrança de alterações pós-lançamento | Alteração solicitada após publicação gera cobrança avulsa registrada | M20 |

### Regra de personalização por plano

| Plano | Grau de personalização do site |
| --- | --- |
| Essencial | Básica — tema e conteúdo |
| Pro | Intermediária — seções e variações visuais |
| Premium | Avançada — layout e identidade |
| Business | Total — máxima liberdade |

A geração inicial e a publicação estão incluídas no plano. **Alterações posteriores ao lançamento são cobradas à parte**, por rodada de alteração.

### Riscos
- Custo de LLM fora de controle. Mitigação: teto por tenant e por requisição, com alerta.
- Cobrança de alteração mal comunicada gerando insatisfação. Mitigação: aviso explícito de custo antes de executar a alteração.

---

## F9 — Escala e operação (3 semanas)

**Objetivo:** preparar a plataforma para operação real.

### Entregas

| # | Entrega | Critério de aceite | Rastreia |
| --- | --- | --- | --- |
| F9.1 | Observabilidade (logs, métricas, trace) | Requisição rastreável de ponta a ponta | U59, N11 |
| F9.2 | Alertas de erro e de indisponibilidade | Alerta disparado em falha simulada | N07 |
| F9.3 | Backup e restauração por tenant | Restauração testada com sucesso | U60 |
| F9.4 | LGPD: consentimento, exclusão e anonimização | Direito do titular atendido | U15, U16, N04 |
| F9.5 | Política de retenção de dados | Dados expirados removidos conforme política | N04 |
| F9.6 | Teste de carga | Meta de 99,5% de disponibilidade validada em cenário controlado | N06, N07, N10 |
| F9.7 | Documentação operacional | Runbook de incidentes publicado | N11 |

---

## Cobertura das funcionalidades já implementadas

Funcionalidades do catálogo que já existem no código atual e são preservadas na migração. Não geram entrega nova; entram como critério de regressão nas fases indicadas.

| Funcionalidade | Descrição | Preservada em |
| --- | --- | --- |
| U02 | Autenticação de usuários | F1.6 |
| U03 | Registro de usuários | F1.2 |
| U04 | Recuperação de senha | F1.6 |
| U05 | Perfil do usuário | F2.1 |
| U06 | Alteração de senha | F1.6 |
| U07 | Exclusão de conta | F1.6 |
| U17 | Cadastro de serviços | F2.3 |
| U18 | Ativação e desativação de serviços | F2.3 |
| U22 | Cadastro de profissionais | F2.4 |
| U23 | Jornada de trabalho | F2.4 |
| U26 | Agenda por profissional | F2.5 |
| U27 | Consulta de disponibilidade | F2.5 |
| U28 | Criação de agendamento | F2.7 |
| U29 | Remarcação | F2.7 |
| U30 | Cancelamento | F2.7 |
| U31 | Conclusão de atendimento | F2.7 |
| U32 | Listagem de agendamentos | F2.7 |
| U14 | Busca e segmentação | F2.2 |
| U37 | Site individual por tenant | F3.1 |
| U43 | Agendamento embutido no site | F3.8 |
| U44 | Galeria de trabalhos | F3.2 |
| U39 | Domínio próprio | Backlog B1 |
| U50 | Preferências de comunicação | F6.2 |

---

## Configurações por segmento

Configurações do catálogo (S01–S12) distribuídas pelas fases. A configuração S11 é entregue em F4.7; as demais entram conforme a fase indicada.

| Configuração | Descrição | Fase |
| --- | --- | --- |
| S01 | Segmento do tenant | F1.1 |
| S02 | Duração padrão e granularidade de horários | F2.7 |
| S03 | Regras de sinal e no-show | F2.9 |
| S04 | Sessões prolongadas e múltiplos blocos | F2.10 |
| S05 | Histórico de procedimentos e fichas | F2.2 |
| S06 | Pacotes de manutenção | F7.2 |
| S07 | Comissões e combos | F5.6 |
| S08 | Múltiplos profissionais por atendimento | F2.5 |
| S09 | Recursos por profissional (sala, maca, cadeira) | F2.6 |
| S10 | Regras de utilização de benefícios | F7.3 |
| S11 | Módulos habilitados por tenant | F4.7 |
| S12 | Identidade visual e tema do site | F3.1 |

### Requisitos não funcionais por fase

| Requisito | Descrição | Fase |
| --- | --- | --- |
| N02 | Segurança (hash, JWT, HTTPS, CSRF) | F1.6 |
| N03 | Autorização por papel e recurso | F1.5 |
| N05 | Auditoria de ações sensíveis | F1.8 |
| N08 | Responsividade | F3.5 |
| N09 | Acessibilidade WCAG 2.1 AA | F3.2 |

---

## Backlog pós-MVP

Itens explicitamente fora do MVP, priorizados depois da validação comercial:

| # | Item | Justificativa |
| --- | --- | --- |
| B1 | Domínio próprio com SSL automatizado | Subdomínio atende o lançamento |
| B2 | Aplicativo móvel nativo | Web responsivo cobre a v1 |
| B3 | Marketplace público de estabelecimentos | Exige base de tenants consolidada |
| B4 | Emissão de nota fiscal | Integração fiscal complexa |
| B5 | Integrações com ERPs | Demanda de clientes maiores |
| B6 | Multi-idioma e multi-moeda | Expansão internacional |
| B7 | Programa de indicação entre tenants | Crescimento após tração |

---

## Critérios de saída do MVP

O MVP é considerado pronto quando:

1. Um novo estabelecimento se cadastra, escolhe plano e entra em trial sem intervenção manual.
2. O estabelecimento cadastra serviços, profissionais e horários.
3. Um cliente agenda pelo site do tenant e recebe confirmação.
4. A equipe gerencia a agenda e conclui atendimentos.
5. A cobrança da assinatura é processada e o tenant inadimplente é suspenso.
6. O isolamento entre tenants é comprovado por testes automatizados no pipeline.
