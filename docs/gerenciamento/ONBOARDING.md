# Onboarding — colocar o primeiro cliente no ar

> **Finalidade:** atender ao critério **F20.2** — documentação do fluxo de onboarding.
> **Público:** quem vai operar a plataforma, não quem desenvolve.
> **Princípio:** cada passo tem verificação. Se a verificação falhar, pare e resolva antes de seguir.

---

## 1. Pré-requisitos

| Item | Como verificar |
| --- | --- |
| Docker em execução | `docker info` responde com a versão |
| Repositório atualizado | `git pull origin main` sem conflito |
| Portas 8080 e 5432 livres | `curl http://localhost:8080/health` falha antes de subir |

---

## 2. Subir o ambiente

```bash
docker-compose up --build -d
```

Aguarde cerca de 2 minutos. O build compila a API e sobe o PostgreSQL.

**Verificação:**

```bash
docker ps
```

Esperado: `servia-api` e `servia-database` com status `healthy`.

```bash
curl http://localhost:8080/health
```

Esperado: `Healthy`.

> Se a API ficar `unhealthy`, veja `docker-compose logs api`. As causas mais comuns estão na seção 3.6 e 3.7 do [RUNBOOK](RUNBOOK.md).

---

## 3. Criar o administrador da plataforma

O administrador da plataforma **não pertence a um estabelecimento**. Ele provisiona tenants e gerencia planos.

```bash
cd src
dotnet run --project Servia.Api/Servia.Api.csproj -- seed-admin \
  --name "Suporte" \
  --email admin@servia.app \
  --password "SenhaSegura123"
```

**Verificação:** a saída deve ser `Administrador criado: admin@servia.app`.

> **Idempotente:** rodar de novo imprime `Administrador da plataforma ja existe` e não duplica.
>
> **Por que não é endpoint:** um endpoint público para criar administrador seria uma porta de escalada de privilégio. É comando de linha, executado por quem tem acesso ao servidor.

---

## 4. Verificar os planos

Os planos são criados automaticamente na inicialização.

```bash
curl http://localhost:8080/api/v1/billing/plans
```

Esperado: quatro planos — Essencial, Pro, Premium e Business.

| Plano | Profissionais | Personalização | IA/mês |
| --- | --- | --- | --- |
| Essencial | até 3 | Básica | 100 |
| Pro | até 7 | Intermediária | 500 |
| Premium | até 15 | Avançada | 1.500 |
| Business | ilimitado | Total | 5.000 |

> **Todos os planos incluem todos os módulos.** A diferença está na faixa de profissionais, no grau de personalização do site e na franquia de IA.

---

## 5. Cadastrar o primeiro cliente

Há dois caminhos. Use o primeiro se o cliente vai se cadastrar sozinho.

### 5.1. Autoatendimento (recomendado)

O cliente acessa a landing page, clica em **Começar agora** e preenche o formulário. O sistema cria o tenant com trial de 14 dias e entra direto no painel.

**Verificação:** o cliente consegue acessar `/painel` e vê a agenda vazia.

### 5.2. Provisionamento pela plataforma

Use quando o cliente não vai se cadastrar sozinho.

```bash
# 1. Autenticar como administrador da plataforma
curl -X POST http://localhost:8080/api/auth/sign-in \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"00000000-0000-0000-0000-000000000000","email":"admin@servia.app","password":"SenhaSegura123"}'

# 2. Provisionar o estabelecimento (use o accessToken da resposta)
curl -X POST http://localhost:8080/api/v1/platform/tenants \
  -H "Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" \
  -d '{
    "slug": "minha-barbearia",
    "name": "Minha Barbearia",
    "segment": 1,
    "document": "12345678000190",
    "planCode": "ESSENCIAL",
    "ownerName": "Nome do Responsável",
    "ownerEmail": "responsavel@exemplo.com",
    "ownerPassword": "SenhaSegura123"
  }'
```

**Verificação:** resposta `201` com o `tenantId`.

---

## 6. Configurar o estabelecimento

O cliente faz isso pelo painel. A ordem importa: a agenda depende de serviços e profissionais.

| Ordem | Página | O que fazer |
| --- | --- | --- |
| 1 | `/painel/servicos` | Cadastrar os serviços com preço e duração |
| 2 | `/painel/equipe` | Cadastrar os profissionais com jornada e dias |
| 3 | `/painel/clientes` | Cadastrar os primeiros clientes (ou deixar o cliente se cadastrar) |
| 4 | `/painel/site` | Criar o site e publicar |
| 5 | `/painel` | Testar um agendamento |

**Verificação:** um agendamento criado em `/painel` aparece na lista do dia.

---

## 7. Verificar o isolamento

Antes de liberar para uso real, confirme que um estabelecimento não vê dados de outro.

```bash
# 1. Autentique em dois tenants diferentes
# 2. Crie um serviço no tenant A
# 3. Liste os serviços no tenant B
```

**Esperado:** o tenant B vê zero serviços. Se vir o serviço do A, **pare** — é falha de isolamento, não de configuração.

---

## 8. Configurar as integrações

Sem credencial, a integração fica desabilitada — a aplicação sobe normalmente.

| Integração | Variável | Sem ela |
| --- | --- | --- |
| Mercado Pago | `MercadoPago__AccessToken` | Cobrança não funciona |
| WhatsApp | `MetaWhatsApp__AccessToken` | Envio falha com motivo explícito |
| OpenAI | `OpenAi__ApiKey` | Geração de site indisponível |
| Webhooks | `MercadoPago__WebhookSecret`, `MetaWhatsApp__WebhookSecret` | Eventos rejeitados com 401 |

> **Segredos nunca no código.** Use variáveis de ambiente ou cofre.

---

## 9. Checklist final

- [ ] `docker ps` mostra API e banco `healthy`
- [ ] `/health` responde `Healthy`
- [ ] Administrador da plataforma criado
- [ ] Quatro planos disponíveis
- [ ] Primeiro cliente cadastrado e consegue entrar
- [ ] Serviços e profissionais cadastrados
- [ ] Site criado e publicado
- [ ] Agendamento de teste criado com sucesso
- [ ] Isolamento verificado entre dois tenants
- [ ] Integrações configuradas (ou conscientemente desabilitadas)

---

## 10. Se algo falhar

| Sintoma | Onde olhar |
| --- | --- |
| API não sobe | `docker-compose logs api`; ver RUNBOOK 3.7 |
| `/health` retorna 400 | RUNBOOK 3.6 (AllowedHosts) |
| Login retorna 401 | Verifique se o tenant existe e o e-mail está correto |
| Cadastro retorna 409 | Slug ou e-mail já usado |
| Cadastro retorna 429 | Limite de tentativas por IP; aguarde 10 minutos |
| Envio de WhatsApp falha | Verifique `MetaWhatsApp__AccessToken` |

O [RUNBOOK](RUNBOOK.md) tem o procedimento completo para cada incidente.
