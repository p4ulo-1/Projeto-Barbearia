# Configuração do Mercado Pago para homologação

A API permite iniciar o Servia com a integração desabilitada. Um Access Token
sozinho permite operações sobre assinaturas existentes. Para iniciar novas
contratações, configure também `MercadoPago:WebhookSecret`, uma
`MercadoPago:ReturnUrl` HTTPS absoluta sem credenciais, query ou fragmento e
os IDs remotos em `MercadoPago:TrialPlanIds`, por código de plano Servia.
A API consulta o plano remoto com o Access Token configurado e confere suas
condições, incluindo cartão de crédito, valor, moeda e trial de 14 dias.

O ambiente do ASP.NET Core controla o carregamento da configuração, inclusive
User Secrets em Development. Ele não determina se uma credencial do Mercado
Pago pertence ao ambiente de Teste ou Produção. A validação local verifica
formato e dependências entre configurações. A API não consegue atestar
localmente se a Public Key compilada no frontend e o Access Token no backend
pertencem à mesma aplicação e ao mesmo ambiente. O prefixo do token não
distingue esses ambientes. O campo `live_mode` da notificação também não serve
para validar a configuração, pois fica fora do manifesto assinado e só chega
depois da operação.

Antes de autorizar o primeiro checkout externo de homologação:

1. Em **Suas integrações → Credenciais**, confirme a aplicação e o seletor
   **Teste**. Configure a Public Key correspondente em
   `VITE_MERCADO_PAGO_PUBLIC_KEY` no build de homologação e o Access Token no
   backend pelo mecanismo de segredos do ambiente. Mantenha as credenciais de
   Produção fora do ambiente de homologação, incluindo variáveis herdadas que
   possam sobrescrever as configurações locais.
2. Confira o segredo de assinatura da aplicação, o destino de webhook do
   modo **Teste** e a URL de retorno do frontend de homologação. Use destinos
   separados dos de Produção.
3. Confira o vínculo de cada código Servia com o plano remoto do contexto de
   teste. Durante a homologação autorizada, valide primeiro a leitura dos planos
   e suas condições. Em caso de divergência ou origem incerta, interrompa a
   homologação antes do envio de qualquer pagamento. Registre apenas a
   aplicação, o ambiente escolhido e o resultado das verificações.

Fontes oficiais

- [Credenciais de Assinaturas](https://www.mercadopago.com.br/developers/pt/docs/subscriptions/additional-content/your-integrations/credentials)
- [Webhooks de Assinaturas](https://www.mercadopago.com.br/developers/pt/docs/subscriptions/additional-content/your-integrations/notifications/webhooks)
