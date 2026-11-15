# Matriz de responsabilidades de controlador e operador — Servia

> Minuta baseada na distinção funcional da LGPD e no ADR-012. Papel não é decidido só pelo contrato: verificar quem determina finalidade e meios essenciais em cada operação.

## 1. Critério de classificação

Controlador decide finalidade e elementos essenciais; operador trata em nome do controlador e segundo instruções (LGPD arts. 5º VI–VII, 39). Uma mesma pessoa jurídica pode ser controladora em uma operação e operadora em outra. O estabelecimento é normalmente controlador dos dados de seus clientes; o Servia, operador desse tratamento. Servia controla conta, billing, segurança e suporte próprios. A qualificação deve corresponder ao fluxo real, conforme Guia da ANPD.

## 2. Matriz por operação

| Operação | Controlador | Operador / fornecedor | Responsabilidades principais |
| --- | --- | --- | --- |
| Cadastro, autenticação e permissões de usuários Servia | Servia | AWS/infra, se contratada; outros fornecedores conforme fluxo | Servia define finalidade, transparência, base, retenção, direitos e segurança; operador hospeda conforme instrução |
| Assinatura e billing do Servia | Servia para cobrança contratual; Mercado Pago atua conforme seu papel no checkout e usos próprios | Mercado Pago pode processar via API; AWS/SES envia avisos | Servia informa preço, base e destinatários; minimiza dados; Mercado Pago aplica termos/regulações próprias; identificar exportações |
| Dados de clientes, agenda, catálogo, profissionais e financeiro do estabelecimento | Tenant | Servia; suboperadores estritamente necessários | Tenant define dados/finalidade/base/transparência, permissões, atendimento e retenção; Servia segue instruções, segurança, assistência, suboperadores e eliminação |
| Site público e agendamento | Tenant para dados do visitante/cliente e publicação de conteúdo; Servia para logs próprios e segurança | Servia e fornecedores de infraestrutura/mensageria | Tenant publica aviso e configura conteúdo; Servia isola tenant e processa reservas conforme instrução; definir IP/cookies/logs do site |
| Conteúdo submetido à IA | Tenant para conteúdo e objetivo do site; Servia para escolhas técnicas/segurança e eventual finalidade própria identificada | DeepSeek ou provedor escolhido, se ativado | Tenant minimiza/instrui; Servia verifica provedor, DPA, local, treinamento, retenção e transferência; provedor pode ser controlador para seus usos próprios segundo termos públicos |
| Envio de email | Tenant para comunicação a seus clientes; Servia para avisos da própria plataforma | Amazon SES/AWS | Tenant autoriza finalidade/listas e descadastro; Servia configura SES, reduz conteúdo e retém logs; AWS opera o serviço conforme DPA/região |
| SMS/WhatsApp ou mensageria | Tenant para conteúdo destinado a seus clientes; Servia para notificações próprias | Twilio se efetivamente contratado; Meta conforme ADR vigente | Tenant garante base e preferências; Servia assegura opt-out, conteúdo mínimo, DPA e geografia; resolver conflito Twilio vs Meta antes do lançamento |
| Suporte e incidentes | Servia para dados de suporte próprio; Tenant para conteúdo de suporte de seus clientes | Operador de ticket/email se houver | Controlador responde a titular; operador encaminha e ajuda; restringir anexos e registrar acesso |
| Segurança, prevenção a fraude e logs | Cada parte para sua infraestrutura e serviços | AWS e demais serviços de hospedagem | Cada agente define base/retention de logs sob seu controle; separar registros legais de acesso da telemetria; manter sigilo e controles |
| Analytics e produto | Servia se define uso analítico próprio; tenant se mede sua operação | infraestrutura analítica não identificada | Anonimizar/agregar antes de reutilização; documentar se dado permanece pessoal |
| Backup, restauração e expurgo | Servia para controles de infraestrutura; tenant mantém dever de controlador sobre dados de clientes | AWS/armazenamento se usado | Servia aplica acesso/retention e replay de exclusões; tenant escolhe instruções e prazos compatíveis; restauração não deve reintroduzir apagados |

## 3. Responsabilidades gerais

### Controlador (Servia ou Tenant conforme operação)

- Documentar finalidade e base legal, fornecer aviso transparente e manter registro de operações (LGPD art.37, com simplificação quando legalmente cabível).
- Avaliar necessidade, proporcionalidade, riscos e tratamento de dados sensíveis/crianças.
- Atender direitos, comunicar incidentes que possam gerar risco ou dano relevante e manter prova das decisões.
- Definir retenção e instruir operadores; autorizar suboperadores/transferências.
- Garantir que instruções e compartilhamentos sejam lícitos.

### Operador (Servia ou fornecedor conforme operação)

- Tratar de acordo com instruções documentadas e dentro do escopo, sem finalidade própria incompatível.
- Manter confidencialidade, controles de segurança, registros e assistência ao controlador.
- Encaminhar solicitações de titulares e autoridades sem demora; não decidir sozinho exceções do controlador.
- Notificar incidentes ao controlador com rapidez suficiente para este cumprir o prazo legal.
- Apagar/devolver dados e vincular suboperadores a obrigações equivalentes.

## 4. Fornecedores: papel e pendências

- **DeepSeek:** política pública declara processamento na China e usos que incluem P&D/treinamento/otimização. Não presumir que é mero operador; confirmar API e contrato e bloquear dados pessoais até salvaguardas.
- **Mercado Pago:** termos de desenvolvedor descrevem cenários em que o usuário é controlador e o desenvolvedor operador; usos de prevenção a fraude/obrigações próprias podem ter papel independente. Mapear o fluxo de checkout de cada parte.
- **AWS SES:** AWS DPA é incorporado aos Service Terms para Customer Data. AWS tende a operador/suboperador no conteúdo de email; região e serviços auxiliares são escolhidos/configurados pelo Cliente.
- **Twilio:** DPA descreve tratamento de Customer Personal Data sob instrução e subprocessadores; role varia para dados de conta/segurança. Documentação do Servia, porém, ainda define Meta direta e WhatsApp pausado. Resolver antes do uso.

## 5. Fontes

LGPD arts. 5, 6, 7, 15, 16, 18, 37, 39, 42; Guia de agentes ANPD; Resoluções 15/2024 (incidentes), 19/2024 (transferência) e 2/2022 (pequeno porte). Documentos internos: ADR-012, ADR-023, ADR-024, ARCH.md, funcionalidades.md e RUNBOOK.md.
