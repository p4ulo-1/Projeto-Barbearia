# Procedimentos para direitos dos titulares e incidentes de segurança

> Procedimento operacional proposto. Designar pessoas, canais, plantão e sistemas antes de entrada em produção. O prazo interno de aviso do operador é mais curto que o prazo externo para permitir triagem; não altera prazo legal.

## Parte A — Solicitações de titulares

### 1. Canais e classificação

Receber pedidos em [formulário/e-mail/endereço]. Registrar data/hora, canal, pedido, agente que recebeu, categoria, tenant e identificadores mínimos. Não exigir acesso a conta como única via. Classificar imediatamente:

1. **Dados próprios do Servia** (conta, contratação, suporte, billing, segurança): Servia controlador; time de privacidade responde.
2. **Dados tratados pelo tenant** (clientes, funcionários, agenda, financeiro do estabelecimento): tenant é controlador; Servia encaminha e presta assistência via painel/canal [●].
3. **Papel incerto ou compartilhado:** preservar o pedido e escalar ao responsável de privacidade; não rejeitar por falta de classificação.

### 2. Verificação e triagem

Confirmar identidade por meios proporcionais ao risco, sem coletar documento integral se método menos invasivo bastar. Representante deve demonstrar poderes quando pertinente. Evitar revelar dados de terceiro. Definir direito solicitado: confirmação/acesso, correção, anonimização/bloqueio/eliminação, portabilidade, informação de compartilhamento, oposição/revogação ou revisão de decisão automatizada. Identificar sistemas ativos, logs, backups e subprocessadores relevantes.

### 3. Prazos e resposta

- A confirmação e o acesso simplificados devem ser providenciados imediatamente quando possível (LGPD art.19 I).
- Declaração clara e completa de confirmação/acesso deve ser fornecida em até 15 dias, nos termos do art.19 II. Regras específicas e cláusulas de transferência podem prever detalhe próprio.
- Pedidos gerais devem receber resposta imediata quando possível; caso contrário, explicar fato/direito impeditivo ou identificar controlador correto (art.18 §4º). A política de 15 dias no RUNBOOK é procedimento interno geral, não deve reduzir direitos imediatos nem confundir todos os pedidos com art.19.
- Agente de pequeno porte só usa prazo em dobro quando satisfaz a Resolução nº 2/2022 e o tipo de solicitação está coberto. Não presumir que o Servia se qualifica por ser startup; verificar critérios e exceções.
- DPA propõe resposta interna pelo Operador ao Cliente em até 5 dias corridos e envio de dados para busca em até 10 dias, para que o controlador responda em prazo legal. Prazos são SLAs internos a aprovar; se risco de estourar prazo, escalar imediatamente.

### 4. Execução por direito

| Pedido | Ação |
| --- | --- |
| Acesso/confirmar | Localizar registros, preparar cópia segura e indicar origem/finalidade/compartilhamentos quando exigido; redigir dados de terceiros |
| Corrigir | Corrigir fonte e propagar a destinatários quando exigível; preservar integridade histórica com evento de retificação |
| Eliminar/anonimizar/bloquear | Avaliar base e exceções do art.16; remover dos sistemas ativos, arquivos, filas e fornecedores; registrar hold quando legítimo; não reintroduzir via backup |
| Oposição/revogação | Cessar o tratamento baseado na base impugnada ou consentimento revogado; analisar base alternativa e informar resultado |
| Portabilidade | Avaliar regulamentação, segredo comercial e dados de terceiros; fornecer formato legível e seguro |
| Reclamação/explicação automatizada | Identificar uso real de decisões unicamente automatizadas; o Servia documenta geração de conteúdo por IA com aprovação humana, mas não há evidência de decisão automatizada com efeito jurídico |
| Dados do tenant | Encaminhar imediatamente ao controlador; auxiliar extração/alteração/expurgo e registrar retorno ao titular pelo canal apropriado |

### 5. Fechamento e registro

A resposta deve ser clara, acessível, gratuita e registrar decisão, sistemas consultados, dados corrigidos/eliminados, fundamento de retenção/exceção, destinatários notificados e data de fechamento. Restringir acesso ao ticket. Se negado total/parcialmente, indicar motivos de fato/direito, canal de revisão e contato da ANPD conforme aplicável.

## Parte B — Incidentes de segurança

### 6. Definição e escalonamento

Tratar como incidente suspeito qualquer acesso, divulgação, alteração, perda, destruição, indisponibilidade ou exfiltração que possa envolver dado pessoal. Qualquer colaborador ou fornecedor deve reportar imediatamente a [canal de plantão]. Exigir aviso contratual de DeepSeek, Mercado Pago, AWS/Amazon SES e Twilio para eventos que afetem dados Servia/tenant; contatos e escalonamento por produto devem ser preenchidos no inventário. Não aguardar confirmação de dano para iniciar contenção.

### 7. Linha do tempo e papéis

**T0 — descoberta:** abrir registro confidencial, marcar horário de ciência, preservar logs, acionar responsável técnico e privacidade.  
**Imediato:** conter acesso/credenciais, isolar sistemas afetados, proteger integridade, avaliar multi-tenancy e informar controlador afetado. Operador (Servia ou fornecedor) deve avisar seu controlador sem demora injustificada; prazo contratual proposto no DPA: até 24 horas da ciência.  
**Primeiras horas:** confirmar categorias de dados, titulares, volume, tenants, países, exfiltração, sensibilidade, vulnerabilidade, impacto físico/moral/financeiro, duração, medidas prévias e risco. Preservar evidência sem ampliar acesso.  
**Antes do limite legal:** o Controlador decide se incidente pode acarretar risco ou dano relevante e, se sim, comunica ANPD e titulares no prazo de três dias úteis contado do conhecimento de que incidente afetou dados pessoais, salvo prazo específico. Comunicação pode ser preliminar e complementada nos termos do regulamento. Para agente de pequeno porte, prazo em dobro somente se preencher requisitos e ressalvas aplicáveis.  
**Após contenção:** corrigir causa, redefinir segredos, restaurar com verificação de exclusões e acompanhar titulares.  
**Registro:** manter todos os incidentes, inclusive não comunicados, por pelo menos cinco anos desde registro (Resolução CD/ANPD nº 15/2024), com acesso restrito.

### 8. Critério de notificação

Controlador documenta avaliação de risco/dano relevante considerando: natureza e volume dos dados; dados financeiros/autenticação/localização/saúde/crianças; facilidade de identificação; extensão e duração; possibilidade de fraude, discriminação, dano físico/moral/econômico; medidas de proteção e mitigação. Dúvida relevante deve ser escalada ao responsável jurídico/privacidade sem perder o prazo.

Comunicação à ANPD e titulares deve incluir, no mínimo, natureza/categorias e quantidade de titulares (incluindo crianças/adolescentes/idosos quando aplicável), medidas de segurança antes/depois, riscos/impactos, motivos de atraso, medidas de mitigação, data de conhecimento e contato do encarregado. Aviso ao titular deve usar linguagem simples e canal direto quando possível; se inviável, divulgar amplamente no sítio/app/canais por pelo menos três meses, e registrar prova nos prazos do regulamento.

### 9. Evidências e preservação

Manter cronologia, sistemas e tenants afetados, logs e hashes, decisões, responsáveis, comunicações, pedidos da autoridade, medidas de contenção/recuperação, impacto e lições aprendidas. Não inserir dados pessoais excessivos no relatório. Preservar cópia forense segregada por necessidade e liberar retenção extraordinária após revisão.

### 10. Exercícios e lacunas operacionais

A documentação do Servia informa que não há logs estruturados, métricas ou tracing suficientes; auditoria existe sem fluxo; não há teste periódico de restauração; e backups integrais multitenant têm retenção padrão de 30 dias sem agendamento confirmado. O procedimento só será executável após designar plantão e encarregado/canal, centralizar logs e evidências, validar acesso cross-tenant, configurar alertas, definir fluxo com fornecedores, manter lista de contatos e simular exercício de incidente.

## Base normativa e referências

- [LGPD](https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm), arts. 18–20, 37, 39, 46 e 48.
- [Resolução CD/ANPD nº 15/2024 — incidente](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/resolucao-cd-anpd-no-15-de-24-de-abril-de-2024); [orientação ANPD sobre comunicação](https://www.gov.br/anpd/pt-br/canais_atendimento/agente-de-tratamento/comunicado-de-incidente-de-seguranca).
- [Resolução CD/ANPD nº 2/2022 — pequeno porte](https://www.gov.br/anpd/pt-br/acesso-a-informacao/institucional/atos-normativos/regulamentacoes_anpd/resolucao-cd-anpd-no-2-de-27-de-janeiro-de-2022).
- [Direitos dos titulares — ANPD](https://www.gov.br/anpd/pt-br/assuntos/titular-de-dados/direito-dos-titulares).
- Documentos internos: docs/operacao/RUNBOOK.md, docs/arquitetura/ARCH.md, docs/arquitetura/ADR.md e DPA.md.

**Aprovação necessária antes de operação:** aprovar contatos, responsáveis e SLA; testar atendimento de direitos e expurgo; definir encarregado (ou justificar dispensa aplicável); confirmar status de agente de pequeno porte; testar notificação aos quatro fornecedores e instruções a tenants.
