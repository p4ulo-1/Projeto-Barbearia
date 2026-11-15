# Política de Privacidade do Servia — minuta v0.1

> **Status: minuta incompleta; publicar somente depois de preencher [●], verificar tecnologias/fornecedores em produção e aprovar os fluxos.** Versão: [●] · Vigência: [●].

## 1. Quem somos e quando esta política se aplica

O Servia é uma plataforma SaaS multi-tenant para negócios de serviços agendados. Esta política explica tratamentos em que a pessoa jurídica fornecedora do Servia decide finalidade e meios essenciais como **controladora**, inclusive cadastro e autenticação de seus representantes, contratação/billing próprio, suporte, segurança e administração da plataforma.

Quando um estabelecimento usa o Servia para tratar dados de seus clientes, funcionários ou profissionais, o estabelecimento é em regra controlador e o Servia operador. Nesse caso, o aviso do estabelecimento e o DPA também se aplicam; encaminhe pedidos sobre esses dados ao próprio estabelecimento. Os papéis são definidos por operação e decisões reais, não apenas pelo contrato.

**Controladora:** [razão social], CNPJ [●], endereço [●]. **Canal de privacidade/encarregado:** [e-mail/formulário/endereço]. Não foi encontrado canal público ou identidade legal nos documentos disponíveis.

## 2. Dados que podemos tratar como controlador

Conforme uso do serviço, poderemos tratar: (a) dados de Owner/contatos administrativos, como nome, e-mail, telefone e credenciais; (b) identificação do estabelecimento e dados de contratação, plano, aceite, alteração e cancelamento; (c) metadados de cobrança, faturas e IDs do gateway, sem CVV e evitando dados integrais de cartão; (d) comunicações de suporte e solicitações de privacidade; (e) dados técnicos mínimos, logs de acesso, dispositivo, IP, tentativas e eventos de segurança; (f) preferências e registros de marketing, se implementados; (g) dados enviados a serviços de IA para geração de site/onboarding, quando o Servia definir uma finalidade própria; e (h) metadados de operação necessários à administração e prevenção a fraude.

A documentação não confirma cookies, pixels, analytics, publicidade comportamental, dados de crianças, valores de faturas, prazos reais ou todos os campos coletados. Não inserir tais práticas nesta política sem inventário técnico.

## 3. Finalidades e bases legais

Tratamos dados, conforme o caso, para: criar/administrar conta e prestar o Serviço (execução de contrato, LGPD art. 7 V); processar pagamentos e cumprir obrigações legais/regulatórias (art. 7 II); proteger conta, prevenir fraude e investigar abuso (art. 7 IX após avaliação documentada, ou outra base aplicável); responder suporte, exercer e defender direitos (art. 7 VI); enviar mensagens operacionais (execução contratual/legítimo interesse conforme conteúdo); e enviar marketing próprio somente com base legal e mecanismo de oposição apropriados.

Não usamos consentimento como autorização genérica para tudo. Para dados sensíveis, aplicam-se as hipóteses taxativas do art. 11; não são necessários para a conta padrão, e não devem ser enviados sem avaliação. Dados inseridos pelo tenant para seus próprios fins são tratados sob instruções documentadas no DPA.

## 4. Compartilhamento e fornecedores

Podemos compartilhar o mínimo necessário com: provedores de infraestrutura e banco de dados; fornecedores de pagamento, e-mail, mensageria e IA; suporte/segurança; assessores profissionais; autoridades quando houver obrigação ou requisição válida; e sucessores em reorganização societária, sujeitos a salvaguardas.

Fornecedores pretendidos/documentados pelo solicitante incluem DeepSeek, Mercado Pago, Amazon SES/AWS e Twilio. A contratação ou uso real, região, entidade contratante e configuração ainda não foram confirmados na documentação. A lista e o papel de cada fornecedor constam no Inventário de Subprocessadores. Mercado Pago pode ser controlador independente para antifraude/obrigações próprias e operador em atividades de API; o papel é contextual. AWS e Twilio publicam adendos de tratamento; DeepSeek declara processar e armazenar na China em sua política pública. A rota via China não será descrita como legalmente regularizada até confirmar entidade, contrato, finalidades, retenção e mecanismo válido de transferência internacional.

Não vendemos dados pessoais como atividade documentada. Qualquer compartilhamento para publicidade precisa ser previamente inventariado e informado.

## 5. Transferências internacionais

Alguns fornecedores podem tratar dados fora do Brasil. Antes de habilitar cada fluxo, identificaremos exportador/importador, países, categorias, finalidades, acesso remoto, suboperadores e mecanismo do art. 33 da LGPD e Resolução CD/ANPD nº 19/2024. A transferência dependerá de decisão de adequação aplicável ou salvaguarda legal válida, incluindo cláusulas-padrão da ANPD quando cabível. Não basta mencionar cláusulas da UE ou uma política geral do fornecedor. A política de DeepSeek informa armazenamento/processamento na República Popular da China; isso exige avaliação e salvaguardas antes de enviar dados pessoais.

## 6. Retenção e segurança

Conservamos dados pelo tempo necessário à finalidade, obrigações legais e defesa de direitos; depois eliminamos ou anonimizamos, exceto hipótese legal de conservação (LGPD arts. 15–16). Os prazos serão descritos por categoria em matriz interna. Os prazos de 5 anos financeiro, 2 anos auditoria, 1 ano operacional, 180 dias mensageria e 90 dias IA registrados no RUNBOOK são parâmetros internos preliminares, não prazos universais impostos pela LGPD. O ciclo de backup documentado é 30 dias, ainda sem agendamento/teste periódico confirmado.

Aplicamos controles proporcionais, incluindo isolamento de tenants, autenticação, hash de senhas e gestão de tokens. A documentação também identifica lacunas: refresh token não está em cookie HttpOnly, CSRF/CORS e rate-limit de login pendentes, auditoria sem fluxo, observabilidade limitada e backups sem restauração periódica. Não apresentamos essas medidas pendentes como já existentes.

## 7. Direitos e como exercê-los

O titular pode solicitar confirmação, acesso, correção, anonimização, bloqueio, eliminação quando cabível, portabilidade nos termos regulamentares, informação sobre compartilhamento, oposição e revisão de decisões automatizadas quando aplicável. Solicitações: [canal]. Podemos pedir informações proporcionais para confirmar identidade e proteger terceiros. Atendimento é gratuito.

Pedidos sobre dados controlados pelo estabelecimento devem ser encaminhados ao estabelecimento. O Servia dará suporte ao controlador, conforme DPA. A confirmação/acesso simplificados são imediatos quando possível; a declaração completa segue art. 19 da LGPD e regulamentos aplicáveis. Pequeno porte só terá prazos diferenciados se preencher os critérios legais e regulamentares.

## 8. Crianças e dados sensíveis

O Serviço é dirigido a estabelecimentos e não foi documentado como produto para crianças. Estabelecimentos não devem inserir dados de crianças ou dados sensíveis além do estritamente necessário e sem base legal, melhor interesse e salvaguardas aplicáveis. A lei ECA Digital e regras vigentes devem ser avaliadas caso haja tratamento de crianças/adolescentes.

## 9. Alterações e contato

Atualizaremos esta política quando mudarem tratamentos relevantes, indicando nova versão e data; alterações que exijam consentimento ou novo aviso serão tratadas conforme lei. Contato do encarregado/canal: [●]. Autoridade Nacional de Proteção de Dados: [canal oficial].

## Fontes e referências públicas consultadas

- [LGPD — Lei nº 13.709/2018](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm).
- [ANPD: agentes de tratamento](https://www.gov.br/anpd/pt-br/centrais-de-conteudo/materiais-educativos-e-publicacoes/guia-orientativo-para-definicoes-dos-agentes-de-tratamento-de-dados-pessoais-e-do-encarregado).
- [ANPD: direitos dos titulares](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados/direito-dos-titulares).
- [Resolução CD/ANPD nº 19/2024 — transferência internacional](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/resolucao-cd-anpd-no-19-de-23-de-agosto-de-2024).
- [Aviso de Privacidade DeepSeek](https://cdn.deepseek.com/policies/en-US/deepseek-privacy-policy.html).
- [Aviso de Privacidade Mercado Pago](https://www.mercadopago.com.br/privacidade/declaracao-privacidade/current).
- [AWS Data Privacy](https://aws.amazon.com/compliance/data-privacy-faq/).
- [Aviso de Privacidade Twilio](https://www.twilio.com/en-us/legal/privacy).

## Pendências

Preencher entidade legal, canais, cookies/telemetria reais, mapa de bases por fluxo, retenção efetiva, suboperadores/regiões, uso ou não de dados para treinamento, transferências e mecanismo, encarregado quando aplicável, e revisão dos avisos do tenant.
