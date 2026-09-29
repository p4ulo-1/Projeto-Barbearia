# New Age

Projeto acadêmico de um sistema de barbearia.

## Tecnologias
- React
- TypeScript
- Vite
- Tailwind CSS

## Sobre
Interface de um sistema de barbearia com:
- Página inicial
- Agendamento
- Login e cadastro
- Área do cliente
- Área administrativa

## Integração local

O frontend usa a API ASP.NET Core para autenticação, serviços, barbeiros, clientes e agendamentos. Configure a URL em um arquivo `.env` local, usando `.env.example` como referência:

```env
VITE_API_URL=http://localhost:5071/api
```

Na ausência da variável, esse mesmo endereço é usado como fallback de desenvolvimento. O navegador mantém no `localStorage` somente o token JWT da sessão; os dados de domínio vêm da API.

> Projeto desenvolvido para fins acadêmicos.
