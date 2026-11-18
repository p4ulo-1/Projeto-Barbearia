# Frontend do Servia

Este é o frontend usado para continuar o desenvolvimento da AV2. `web/` permanece como referência até a substituição operacional. O Marco A conecta esta aplicação ao backend existente; onboarding, gestão e cobrança ainda seguem o `docs/private/planejamento/ESCOPO_AV2.md`.

## Desenvolvimento local

Use Node.js 22 (arquivo `.nvmrc`) e npm. O `package-lock.json` é o lockfile adotado para instalação e CI; `bun.lock` foi preservado como parte da entrega original, mas não é usado no fluxo oficial.

```powershell
cd frontend
npm ci
npm run dev
```

Inicie a API separadamente com seu PostgreSQL de desenvolvimento. O perfil HTTP de `src/Servia.Api/Properties/launchSettings.json` usa `http://localhost:5222`; o Vite encaminha `/api` para esse endereço. Se usar o Docker Compose do repositório, a API fica em `http://localhost:8080` e o alvo do proxy deve ser definido **antes** de iniciar o Vite:

```powershell
$env:SERVIA_API_PROXY_TARGET="http://localhost:8080"
npm run dev
```

Por padrão o cliente usa a API real na mesma origem. `VITE_API_URL` pode apontar para outra origem, mas isso exige CORS apropriado na API; o proxy local evita essa exigência. Não há troca silenciosa para dados simulados. Para abrir a demonstração isolada, defina explicitamente `VITE_DEMO_MODE=true` antes de iniciar o Vite. `.env.example` documenta as variáveis, sem credenciais; arquivos de ambiente locais são ignorados pelo Git.

O login real exige ID do estabelecimento, e-mail e senha de um usuário de desenvolvimento. Não use dados de produção nos testes. O editor de site e a cobrança ainda têm dependências AV2 registradas no plano de integração; uma tela renderizada não comprova persistência no backend.

## Verificações

```powershell
npm test
npm run typecheck
npm run build
```

O CI executa essas verificações após `npm ci`. As rotas básicas devem responder também após iniciar `npm run dev`; o build gera `.output/`, que é ignorado pelo Git.

Para verificar proxy, login, rotação do refresh token, escrita/leitura persistida, disponibilidade privada por `serviceId` e logout contra **API e banco locais isolados**, use outro terminal após iniciar o Vite. O comando cria estabelecimento, cliente, serviço e profissional sintéticos no banco de teste; não o execute contra dados compartilhados ou de produção.

```powershell
$env:SERVIA_SMOKE_BASE_URL="http://127.0.0.1:8081"
npm run smoke:api
```

Use a porta indicada pelo Vite, pois ela pode variar se `8080` já estiver ocupado pela API Docker.

## Limites após o Marco A

- AV2-04/16: a agenda própria do profissional depende de um contrato autorizado no backend; a lista atual exige `ViewTeamAgenda`, enquanto o papel possui `ViewOwnAgenda`. A interface ainda infere o profissional por um compromisso existente.
- AV2-06/07/20: gestão completa da equipe, jornada e transições de limite seguem o escopo principal. Alterações locais do backend ainda não consolidadas afetam essas regras.
- AV2-10–14: os quatro templates existem na interface, mas o editor ainda usa IndexedDB; o onboarding e a publicação determinísticos dependem dos contratos AV2.
- AV2-09/19–21: a interface comercial e os endpoints locais de trial não comprovam contratação recorrente Mercado Pago; os estados de pagamento e a régua de inadimplência são trabalho do escopo principal.
- AV2-15/17/18: domínio público, links antigos e quatro fluxos completos não foram homologados nem fazem parte do aceite de adoção para desenvolvimento.

O `npm run lint` ainda encontra erros de formatação herdados da entrega original em arquivos fora das correções do Marco A; por isso o CI inicial valida testes, tipos e build. Os arquivos alterados para a integração foram verificados com ESLint individualmente.
