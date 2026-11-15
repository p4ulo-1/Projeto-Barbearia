# Acordo de Tratamento de Dados Pessoais (DPA) — minuta v0.1

> **Anexo aos Termos de Uso. Minuta sujeita a preenchimento e revisão.** Para transferências internacionais não use este texto como substituto das cláusulas-padrão brasileiras: anexar e incorporar integralmente o texto obrigatório do Anexo II da Resolução CD/ANPD nº 19/2024, preenchido para as partes, quando esse for o mecanismo escolhido.

## 1. Partes, hierarquia e vigência

**Controlador/Cliente:** estabelecimento identificado no formulário de contratação, seus dados de contato e encarregado: [●]. **Operador:** [razão social Servia], CNPJ [●], endereço [●], contato de privacidade [●]. Este DPA integra os Termos de Uso e vigora enquanto o Operador tratar dados pessoais em nome do Controlador. Para conflito sobre dados, prevalecem normas imperativas da LGPD, cláusulas-padrão internacionais aplicáveis e este DPA; não limita direitos dos titulares nem responsabilidade legal.

Este instrumento se aplica somente a tratamentos em que o Cliente determina finalidade e meios essenciais e o Servia trata por sua conta. O Servia atua como controlador separado para conta, cobrança, segurança e suporte próprios. Cada parte qualifica o papel conforme fatos reais.

## 2. Objeto, finalidade e instruções

O Operador tratará dados para hospedar e operar a plataforma Servia e as funções contratadas: cadastro e gestão de clientes, equipe, profissionais, catálogo, agenda, histórico, site, suporte, financeiro do estabelecimento, comunicação e geração de conteúdo de IA, conforme recursos realmente ativados.

Instruções documentadas são: (a) configurar/usar os recursos contratados; (b) receber e atender instruções razoáveis do Cliente pelo painel ou canal [●]; (c) cumprir Termos e DPA; (d) realizar instrução exigida por lei, notificando o Cliente antes salvo proibição legal. O Operador avisará se considerar que instrução viola a LGPD; poderá suspender a instrução contestada até acordo, sem usar os dados para finalidade própria.

## 3. Categorias e titulares

**Titulares:** clientes finais do estabelecimento; Owner, administradores, funcionários, profissionais e usuários; pessoas mencionadas em agenda, anexos ou comunicações. **Dados:** identificação e contato; conta e permissões; agendamentos, serviços e preferências; dados financeiros/comerciais; conteúdo de sites, prompts, arquivos e suporte; metadados técnicos e logs. **Sensíveis:** podem ser acidentalmente inseridos em observações, documentos, imagens ou prompts (inclusive saúde); o Cliente deve evitar isso ou documentar finalidade, hipótese do art. 11 e instrução específica. **Crianças:** não previsto como uso padrão; não inserir sem avaliação jurídica, melhor interesse e medidas específicas.

## 4. Deveres do Controlador

O Controlador: (a) define finalidade, base legal e retenção; (b) fornece avisos aos titulares e atende direitos; (c) garante licitude, qualidade e minimização; (d) instrui o Operador e autoriza dados sensíveis/transferências quando aplicável; (e) configura permissões e remove acessos; (f) escolhe recursos/provedores adequados; (g) não inclui dados além do necessário; e (h) avalia se tratamento de alto risco exige RIPD ou consulta à ANPD.

## 5. Deveres do Operador

O Operador: (a) trata dados apenas conforme instruções e para prestar/segurar o Serviço; (b) assegura confidencialidade de pessoal autorizado; (c) mantém registro e medidas de segurança proporcionais, nos limites do art. 39; (d) presta assistência razoável a direitos, RIPD, consultas e conformidade, considerando a natureza do tratamento e as informações disponíveis; (e) informa ao Controlador pedidos recebidos diretamente, sem responder em seu nome salvo instrução; (f) coopera com ANPD nos limites legais; (g) informa impedimento legal de atender instrução; e (h) não vende nem usa dados do Cliente para publicidade própria ou treinamento de modelos, exceto se um fornecedor terceiro tiver termos próprios: nesse caso, o Operador não o habilitará para dados pessoais até obter salvaguardas compatíveis e autorização do Controlador.

## 6. Segurança

O Operador manterá medidas técnicas e administrativas adequadas ao risco, incluindo controle de acesso por função, autenticação, isolamento de tenant, gestão de credenciais, criptografia em trânsito e, quando disponível, repouso, gestão de vulnerabilidades, backup, resposta a incidentes e confidencialidade. O desenho documentado inclui filtro global, chaves compostas, hash PBKDF2 e tokens com rotação. Pendências registradas incluem CSRF/CORS e cookie HttpOnly, rate limit de login, trilha de auditoria funcional, observabilidade e validação de restauração; tais controles não são garantidos como implantados até sua verificação.

O Cliente protege seus próprios endpoints, credenciais, dispositivos, permissões e cópias exportadas. As partes não garantem segurança absoluta, mas não afastam deveres legais.

## 7. Suboperadores

O Controlador concede autorização geral apenas aos subprocessadores listados no Inventário vigente, sujeito a atualização e ao direito de objeção fundamentada em [15] dias, antes da nova contratação quando possível. O Operador avisará mudanças no canal [●]. Se a objeção não puder ser resolvida por alternativa razoável, as partes discutirão suspensão do fluxo afetado ou encerramento do módulo/contrato, sem penalidade adicional por esse motivo.

O Operador vinculará suboperadores por contrato com proteção equivalente, limitará acesso, monitorará obrigações e continuará responsável perante o Controlador pelo desempenho contratual do suboperador, sem afastar a responsabilidade prevista em lei. Inventário atual declara fornecedores pretendidos; status ativo, entidade, escopo e contrato ainda precisam ser confirmados.

## 8. Transferência internacional

Nenhum dado será transferido ou acessado de fora do Brasil sem identificar países, importador, finalidades, suboperadores e mecanismo válido conforme LGPD art. 33 e Resolução nº 19/2024. Onde aplicável, as partes incorporarão integralmente as cláusulas-padrão da ANPD, com anexos preenchidos, sem modificar seu texto obrigatório. DeepSeek informa armazenamento/processamento na China; esse fluxo fica **bloqueado para dados pessoais identificáveis** até a confirmação do serviço/API, contrato vinculante, tratamento para treinamento, retenção, suboperadores e salvaguarda brasileira válida. Políticas públicas não substituem DPA ou mecanismo de transferência.

## 9. Direitos dos titulares

O Operador dará assistência técnica e encaminhará ao Controlador, sem demora indevida, solicitações relacionadas aos dados sob este DPA. O Controlador decide resposta e cumpre prazos legais. O Operador apoiará busca, exportação, correção, bloqueio, anonimização e eliminação, conforme capacidade contratada. Custos extraordinários [●] devem ser acordados previamente e não podem impedir exercício gratuito do direito pelo titular.

## 10. Incidentes

O Operador notificará o Controlador **sem demora injustificada e, como compromisso contratual, em até 24 horas** após ciência razoável de incidente que afete dados sob sua responsabilidade, mesmo se ainda não for possível concluir risco relevante. A notificação inicial conterá fatos conhecidos, categorias/volume, tenants/titulares possivelmente afetados, medidas tomadas e contato; atualizações serão enviadas conforme apuração.

O Controlador, como responsável pela comunicação legal, avalia risco/dano relevante e, quando exigido, comunica ANPD e titulares no prazo de três dias úteis da ciência, ressalvada regra específica. Prazo em dobro só se aplica se o controlador efetivamente preencher requisitos de agente de pequeno porte e exceções legais/regulatórias não afastarem a extensão. As partes cooperam na investigação, contenção, preservação de evidências e mitigação. O Operador não notificará titulares em nome do Controlador sem instrução, salvo dever legal.

## 11. Retenção, devolução e eliminação

Ao terminar a prestação, o Operador disponibilizará exportação por [●] dias e, depois, eliminará ou devolverá dados pessoais conforme escolha documentada do Controlador, exceto dados que deva conservar por lei ou exercício regular de direitos, isolados e bloqueados a outros usos. Cópias de segurança expiram no ciclo informado de [●] dias; quando restauração ocorrer, o Operador reaplicará instruções de eliminação pendentes antes de reabrir acesso. Os 30 dias do RUNBOOK são configuração documental não validada tecnicamente; preencher após teste.

## 12. Auditoria e demonstração

Mediante solicitação razoável, o Operador fornecerá informações necessárias para demonstrar cumprimento e permitirá auditoria documental proporcional, com aviso de [30] dias, confidencialidade e sem acesso a dados de outros clientes ou segredos. Certificações/relatórios e frequência: [●]. Não há certificação ou relatório de auditoria documentado nos arquivos disponíveis.

## 13. Responsabilidade e encerramento

Cada parte responde conforme LGPD, contrato e sua contribuição causal. Nada limita direitos de titulares, competência da ANPD ou responsabilidade legal. Rescisão dos Termos encerra este DPA, sem prejuízo de exportação, eliminação, confidencialidade e obrigações legais sobreviventes.

## Anexo A — detalhes do tratamento

| Elemento | Descrição |
| --- | --- |
| Objeto | Hospedagem/operação de sistema SaaS multi-tenant |
| Duração | Vigência do contrato e período de exportação/expurgo definido acima |
| Finalidades | Gestão do estabelecimento, clientes, profissionais, agenda, catálogo, site, financeiro, comunicação, suporte e IA ativada |
| Titulares | Clientes finais, Owner, usuários, funcionários, profissionais e pessoas mencionadas em conteúdo |
| Dados | Identificação, contato, conta/permissões, agenda, preferências, financeiro, conteúdo, arquivos e metadados técnicos |
| Sensíveis | Não previstos como uso necessário; podem ser inseridos indevidamente em observações/imagens/prompts |
| Operações | Coleta, hospedagem, organização, consulta, transmissão a fornecedores, geração de conteúdo, suporte, backup, exportação e eliminação |
| Países | Brasil para serviço primário não confirmado; cada fornecedor/Região deverá ser preenchido no inventário |
| Retenção | Por instrução e matriz de retenção do Cliente; política final ainda não aprovada |

## Anexo B — suboperadores

Ver Inventário em docs/INVENTARIO_FORNECEDORES_SUBPROCESSADORES.md. Fornecedores declarados: DeepSeek (IA), Mercado Pago (pagamentos), AWS/Amazon SES (e-mail) e Twilio (mensageria). Nenhum deve receber dados de produção até validar função, produto, entidade, região, DPA e transferência; Twilio conflita com ADR-023 sobre Meta direta.

## Referências públicas para estrutura

O DPA público do [Calendly](https://calendly.com/legal/data-processing-addendum) e os DPAs de [Twilio](https://www.twilio.com/en-us/legal/data-protection-addendum) e [AWS](https://d1.awsstatic.com/legal/aws-dpa/aws-dpa.pdf) foram consultados como referência estrutural. A minuta foi redigida para o contexto Servia, sem copiar cláusulas desses documentos. A Resolução CD/ANPD nº 19/2024 e seu Anexo de cláusulas-padrão brasileiras prevalecem quando aplicáveis.

## Pendências para assinatura

Identificar partes/canais; decidir prazo de objeção; testar exportação e expurgo; anexar cláusulas ANPD quando necessárias; obter DPAs e regiões; avaliar DeepSeek; conciliar Twilio com ADR-023; documentar medidas efetivamente implementadas; negociar auditoria, custo e SLA.
