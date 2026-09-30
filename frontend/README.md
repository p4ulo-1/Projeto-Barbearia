# New Age Barbearia — Frontend

Frontend da aplicação web do projeto acadêmico **New Age Barbearia**, desenvolvido para a disciplina de Design e Programação Orientados a Objetos.

O frontend fornece a interface para clientes e administradores e se comunica com o backend por meio de uma API REST desenvolvida em ASP.NET Core.

---

## 🛠️ Tecnologias

* React
* TypeScript
* Vite
* TanStack Router
* TanStack Start
* Tailwind CSS
* Radix UI
* React Hook Form
* Zod
* Bun

---

## 📁 Estrutura

```text
frontend/
├── public/
├── src/
│   ├── components/
│   ├── lib/
│   ├── routes/
│   └── ...
├── package.json
├── bunfig.toml
├── vite.config.ts
└── README.md
```

---

## 💈 Funcionalidades

A interface contempla:

* página inicial;
* cadastro;
* login;
* autenticação de usuários;
* área do cliente;
* consulta de serviços;
* consulta de barbeiros;
* consulta de disponibilidade;
* criação de agendamentos;
* visualização de agendamentos;
* remarcação;
* cancelamento;
* conclusão de atendimento;
* área administrativa;
* gerenciamento de clientes;
* gerenciamento de serviços.

Os dados exibidos pela aplicação são obtidos da API do backend.

---

## 🔌 Integração com o Backend

O frontend utiliza a API ASP.NET Core localizada em:

```text
../backend/BackAndre.Api/
```

A URL da API pode ser configurada utilizando a variável de ambiente:

```env
VITE_API_URL=http://localhost:5071/api
```

Utilize `.env.example` como referência para a configuração local.

Na ausência da variável, o endereço:

```text
http://localhost:5071/api
```

é utilizado como fallback de desenvolvimento.

### Fluxo da aplicação

```text
React
  ↓
HTTP / JSON
  ↓
BackAndre.Api
  ↓
Application Services
  ↓
Repositories
  ↓
SQLite
```

---

## 🔐 Sessão e autenticação

A autenticação é realizada pelo backend utilizando JWT.

Após o login, o frontend utiliza o token recebido para realizar chamadas autenticadas à API.

O navegador mantém no `localStorage` somente o token JWT necessário para a sessão.

Os dados de domínio, como:

* usuários;
* serviços;
* barbeiros;
* agendamentos;

são obtidos da API e não são mantidos como fonte principal de dados no frontend.

---

## ▶️ Executando

### 1. Inicie o backend

A partir da raiz do projeto:

```powershell
dotnet run --project .\backend\BackAndre.Api\BackAndre.Api.csproj
```

A API ficará disponível em:

```text
http://localhost:5071
```

Swagger:

```text
http://localhost:5071/swagger
```

### 2. Instale as dependências do frontend

Dentro da pasta `frontend`:

```powershell
bun install
```

### 3. Inicie o frontend

```powershell
bun run dev
```

O Vite exibirá no terminal o endereço local utilizado pela aplicação.

---

## ⚙️ Configuração local

Crie um arquivo `.env` na pasta `frontend` quando for necessário alterar a URL da API:

```env
VITE_API_URL=http://localhost:5071/api
```

O arquivo `.env` é específico do ambiente local e não deve conter informações sensíveis destinadas ao versionamento.

---

## 🌐 CORS

O backend possui configuração de CORS para permitir o acesso do frontend durante o desenvolvimento.

As origens de desenvolvimento atualmente configuradas incluem:

```text
http://localhost:3000
http://localhost:5173
http://localhost:8080
```

Caso o frontend seja executado em outra origem, a configuração de CORS do backend deverá ser ajustada.

---

## 🧪 Validação

Para validar a integração:

1. iniciar o backend;
2. verificar o Swagger;
3. iniciar o frontend;
4. realizar login;
5. consultar serviços e barbeiros;
6. consultar disponibilidade;
7. criar um agendamento;
8. consultar os agendamentos;
9. testar remarcação e cancelamento;
10. validar os fluxos administrativos disponíveis.

---

## 🎓 Projeto Acadêmico

Este frontend faz parte do projeto acadêmico **Projeto-Barbearia**, desenvolvido para a disciplina de **Design e Programação Orientados a Objetos** do curso de **Ciência da Computação — UNIFESO**.

