# Algumas diretrizes e Conclusões sobre o Servia

## 1. Escopo e abrangência do produto

O Servia é concebido como uma plataforma SaaS completa, destinada a negócios que trabalham com serviços agendados.

Embora os diferentes segmentos apresentem particularidades, grande parte das funcionalidades necessárias é compartilhada. Portanto, a proposta é desenvolver uma plataforma com um núcleo universal, capaz de atender às necessidades comuns dos estabelecimentos, permitindo adaptações específicas conforme o segmento e o modelo de negócio.

A existência de funcionalidades semelhantes entre os diferentes segmentos favorece a reutilização de componentes, regras e estruturas, evitando a necessidade de desenvolver aplicações independentes para cada tipo de estabelecimento.

## 2. Núcleo universal e particularidades dos segmentos

A arquitetura do domínio deverá considerar a existência de conceitos comuns aos diferentes segmentos, como:

- Clientes.
- Profissionais.
- Serviços.
- Agendamentos.
- Pagamentos.
- Disponibilidade e jornadas de trabalho.

As particularidades de cada segmento deverão ser implementadas por meio de módulos, políticas de negócio e comportamentos especializados.

Essa abordagem permite preservar um núcleo comum sem obrigar todos os estabelecimentos a trabalhar com regras idênticas.

O uso de interfaces, abstrações e polimorfismo deverá permitir que diferentes implementações compartilhem contratos comuns, mantendo comportamentos específicos quando necessário.

O objetivo é evitar que o núcleo do sistema acumule condicionais e regras específicas para identificar o segmento de cada estabelecimento.

## 3. Arquitetura e segurança multi-tenant

O sistema deverá operar sobre uma infraestrutura compartilhada, mantendo ambientes logicamente independentes para cada estabelecimento.

O isolamento entre tenants é um requisito fundamental e inegociável.

**Nenhum usuário de um estabelecimento poderá acessar dados pertencentes a outro estabelecimento, independentemente de seu papel, nível de autorização ou privilégios dentro do próprio tenant.**

A implementação deverá considerar os seguintes mecanismos:

1. **Resolução confiável do tenant no servidor:** o contexto do estabelecimento deverá ser identificado e validado no backend, sem depender de informações fornecidas livremente pelo frontend.

2. **Validação de pertencimento do usuário ao tenant:** toda operação deverá verificar se o usuário autenticado pertence ao estabelecimento no qual está tentando atuar.

3. **Verificação de TenantId nas operações de escrita:** inclusões, alterações e exclusões deverão respeitar o contexto do estabelecimento.

4. **Restrições de integridade no banco de dados:** relacionamentos e operações de persistência deverão impedir associações indevidas entre registros de diferentes estabelecimentos.

5. **Testes automatizados de isolamento:** deverão existir testes que tentem acessar, modificar e excluir dados de outros tenants, confirmando que essas operações são rejeitadas.

6. **Tratamento específico para administradores da plataforma:** os privilégios administrativos deverão ser diferenciados das permissões dos usuários dos estabelecimentos, com controle explícito das operações administrativas.

O isolamento deverá ser aplicado de maneira consistente em todas as camadas da aplicação, incluindo API, serviços, domínio e persistência.

### 3.1. Identidade dos usuários por estabelecimento

Os usuários serão cadastrados individualmente em cada estabelecimento.

Uma mesma pessoa física poderá frequentar diferentes estabelecimentos, mas possuirá cadastros independentes em cada um deles.

Por exemplo:

- Na Barbearia XYZ, João das Coves será identificado como cliente da Barbearia XYZ.
- No Salão ABC, João das Coves será identificado como cliente do Salão ABC.

Embora seja a mesma pessoa física, o sistema tratará os dois registros como usuários distintos, sem estabelecer automaticamente qualquer relacionamento entre eles.

Essa decisão reforça o isolamento entre tenants e evita a necessidade de manter uma identidade global compartilhada entre os estabelecimentos.

## 4. Agendamento e disponibilidade

O agendamento deverá funcionar com base na disponibilidade individual de cada prestador de serviço.

Cada profissional possuirá sua própria agenda, definida a partir de sua jornada de trabalho, horários disponíveis e eventuais bloqueios.

O cliente poderá realizar um agendamento quando o profissional estiver disponível no dia e horário desejados.

Uma vez realizada uma reserva, o período correspondente deverá ficar indisponível para outros agendamentos incompatíveis.

O sistema deverá considerar:

- Disponibilidade individual dos profissionais.
- Duração de cada serviço.
- Horários de trabalho e intervalos.
- Bloqueios de agenda.
- Cancelamentos e remarcações.
- Estados e transições dos agendamentos.
- Prevenção de reservas conflitantes.

### 4.1. Concorrência nos agendamentos

Um cuidado técnico importante é impedir que duas solicitações simultâneas reservem o mesmo horário.

A verificação de disponibilidade não poderá depender exclusivamente de uma consulta realizada antes da gravação do agendamento.

Deverão ser adotadas estratégias de controle de concorrência, garantindo que apenas uma operação incompatível seja confirmada.

As possibilidades incluem transações no banco de dados, bloqueios apropriados, restrições de integridade e mecanismos de controle de concorrência.

O resultado esperado é garantir a consistência da agenda mesmo quando diferentes clientes tentarem reservar o mesmo profissional e horário simultaneamente.

## 5. Sites individuais dos estabelecimentos

Cada estabelecimento deverá possuir um site próprio, desenvolvido a partir de modelos de frontend previamente estabelecidos.

A personalização deverá ocorrer por meio de configurações, sem necessidade de desenvolver uma aplicação independente para cada estabelecimento.

O sistema deverá permitir personalizar elementos como:

- Cores e identidade visual.
- Logotipo e imagens.
- Textos e descrições.
- Organização das seções.
- Apresentação dos serviços.
- Apresentação dos profissionais.
- Informações de contato.
- Elementos de divulgação e agendamento.

O backend deverá permanecer independente das escolhas de personalização visual. As alterações realizadas pelo estabelecimento deverão afetar apenas a configuração e a apresentação do site.

## 6. Criação e personalização de sites com IA

A interface de criação e personalização dos sites deverá funcionar por meio de um chatbot com inteligência artificial.

O proprietário conversará com a IA, descrevendo suas preferências, fornecendo informações sobre o estabelecimento e indicando como imagina seu site.

Por trás dessa interação, a IA deverá adequar as escolhas do proprietário aos modelos de frontend disponíveis, sem gerar ou modificar diretamente o código-fonte da aplicação.

O processo deverá seguir as seguintes etapas:

1. O proprietário informa os dados do estabelecimento e fornece as imagens necessárias.
2. A IA sugere textos, cores, imagens e organização do conteúdo.
3. O sistema adequa as sugestões às estruturas e aos modelos de código previamente disponíveis.
4. O proprietário visualiza uma prévia do site personalizado.
5. O proprietário aprova a configuração e realiza a publicação.

### 6.1. Configuração estruturada

A IA deverá produzir ou modificar um objeto de configuração estruturado, que será interpretado pelo frontend.

Esse objeto deverá conter as informações necessárias para personalizar o site, como identidade visual, conteúdo, imagens e organização das seções.

O sistema será responsável por validar as configurações e garantir que elas sejam compatíveis com os modelos disponíveis.

**A IA não deverá gerar código React, alterar diretamente os arquivos do projeto ou modificar a estrutura da aplicação.**

Essa decisão mantém a personalização controlada, permite reutilizar os modelos existentes e reduz os riscos de incompatibilidade e comportamentos inesperados.

## 7. Integrações com serviços externos

As integrações com WhatsApp e serviços de pagamento fazem parte das funcionalidades necessárias do produto.

Elas deverão ser consideradas no planejamento completo da solução, não sendo tratadas como funcionalidades dispensáveis.

### 7.1. WhatsApp

A plataforma deverá oferecer automação de comunicação e atendimento por meio do WhatsApp.

A utilização deverá respeitar os limites estabelecidos para cada plano contratado.

Será necessário considerar:

- Integração com a API ou provedor de WhatsApp.
- Envio de notificações e mensagens automatizadas.
- Confirmações e lembretes de agendamentos.
- Comunicação com clientes.
- Controle de consumo por estabelecimento.
- Limites de utilização conforme o plano.
- Custos associados ao envio de mensagens.
- Tratamento de falhas e respostas externas.

A política comercial para utilização acima do limite contratado ainda deverá ser definida, incluindo a possibilidade de aquisição de extensões de consumo, valores adicionais e condições de contratação.

O sistema deverá possuir mecanismos para controlar o consumo e impedir que os limites de cada plano sejam ultrapassados sem a autorização correspondente.

### 7.2. Pagamentos

A plataforma deverá oferecer recursos de integração com serviços de pagamento, contemplando tanto as operações financeiras dos estabelecimentos quanto as cobranças realizadas pelo próprio Servia.

A implementação deverá considerar:

- Confirmação de transações.
- Processamento de pagamentos.
- Webhooks de confirmação e atualização.
- Tratamento de falhas de comunicação.
- Prevenção de processamento duplicado.
- Cancelamentos e estornos.
- Conciliação financeira.
- Registro e rastreabilidade das operações.

Também será necessário garantir que as operações financeiras sejam associadas corretamente ao estabelecimento responsável.

## 8. Separação entre financeiro do estabelecimento e billing da plataforma

O sistema deverá manter dois contextos financeiros independentes.

### 8.1. Financeiro do estabelecimento

Representa os recursos oferecidos pelo Servia aos seus clientes para administrar as finanças de seus próprios negócios.

Poderá contemplar:

- Registro de receitas e despesas.
- Controle de pagamentos recebidos.
- Gestão de comissões dos profissionais.
- Controle financeiro de serviços prestados.
- Gestão de pacotes e clubes de assinatura.
- Relatórios financeiros.
- Indicadores de desempenho.

### 8.2. Billing da plataforma

Representa a relação comercial entre o Servia e os estabelecimentos que contratam seus serviços.

Poderá contemplar:

- Cadastro e gerenciamento de planos.
- Cobranças recorrentes.
- Assinaturas.
- Controle de inadimplência.
- Cancelamentos.
- Taxas sobre transações.
- Histórico de cobranças.
- Controle de utilização e limites dos planos.

Esses dois contextos deverão possuir responsabilidades, regras de negócio e registros independentes, evitando misturar a gestão financeira dos estabelecimentos com a administração comercial da plataforma.

## 9. Testes e confiabilidade

A qualidade e a consistência do sistema deverão ser verificadas por meio de testes automatizados em diferentes níveis.

Os testes deverão contemplar tanto as regras de negócio quanto a integração entre os componentes da aplicação.

Os seguintes critérios são considerados obrigatórios:

| Área | Teste necessário |
|---|---|
| Domínio | Regras de negócio e invariantes |
| Agendamento | Conflitos e concorrência |
| Multi-tenancy | Tentativas de acesso cruzado |
| Autorização | Permissões de cada papel |
| API | Integração entre endpoints e banco de dados |
| Migração | Preservação dos dados existentes, quando aplicável |
| Frontend | Fluxos principais de utilização |
| Integrações | Falhas, repetição e respostas externas |

Os testes de multi-tenancy deverão comprovar que usuários de um estabelecimento não conseguem consultar ou modificar dados de outros estabelecimentos.

Os testes de agendamento deverão demonstrar que o sistema impede reservas conflitantes, inclusive em situações de concorrência.

As integrações externas deverão ser testadas considerando respostas inesperadas, indisponibilidade dos serviços e repetição de solicitações.

O objetivo é garantir que o comportamento do sistema permaneça consistente mesmo diante de falhas e situações excepcionais.

## 10. Princípios gerais de implementação

As decisões e conclusões desta análise estabelecem os seguintes princípios para o desenvolvimento do Servia:

1. **Núcleo universal:** funcionalidades compartilhadas deverão ser implementadas de maneira reutilizável, sem criar aplicações independentes para cada segmento.

2. **Especialização por segmento:** particularidades deverão ser tratadas por módulos, políticas de negócio e comportamentos especializados, utilizando abstrações e polimorfismo quando apropriado.

3. **Isolamento absoluto entre tenants:** nenhum usuário de um estabelecimento poderá acessar dados de outro estabelecimento.

4. **Identidade independente por estabelecimento:** os cadastros de usuários serão específicos de cada tenant, mesmo quando representarem a mesma pessoa física.

5. **Personalização controlada:** os sites individuais serão construídos a partir de modelos predefinidos, com personalização baseada em configurações.

6. **IA como mecanismo de personalização:** a inteligência artificial auxiliará na interpretação das preferências do proprietário e na configuração dos modelos existentes, sem gerar ou modificar diretamente o código da aplicação.

7. **Separação de contextos financeiros:** o financeiro dos estabelecimentos e o billing da plataforma serão tratados como domínios independentes.

8. **Integrações como parte do produto:** WhatsApp e pagamentos deverão integrar a solução, com controle de utilização, custos e tratamento de falhas.

9. **Confiabilidade e consistência:** regras de negócio, isolamento de dados, agendamentos e integrações deverão possuir mecanismos de validação e testes automatizados.

---

**Conclusão:** O Servia deverá ser desenvolvido como uma plataforma SaaS multi-tenant, modular e extensível, capaz de atender diferentes segmentos de negócios de serviços por meio de um núcleo universal e especializações controladas. A personalização dos estabelecimentos, inclusive a criação de sites com auxílio de inteligência artificial, deverá ocorrer sem comprometer a estrutura central da aplicação, a integridade dos dados ou o isolamento entre os clientes da plataforma.