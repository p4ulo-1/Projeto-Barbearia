# 💈 Projeto-Barbearia

<p align="center">
  <strong>Projeto Integrador — Design e Programação Orientados a Objetos</strong>
</p>

<p align="center">
  Aplicação web para gerenciamento de uma barbearia, desenvolvida como projeto acadêmico colaborativo.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Em%20Desenvolvimento-yellow?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Projeto-Acad%C3%AAmico-blue?style=for-the-badge" />
  <img src="https://img.shields.io/badge/React-TypeScript-61DAFB?style=for-the-badge&logo=react&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-ASP.NET%20Core-512BD4?style=for-the-badge&logo=.net&logoColor=white" />
</p>

---

## 🎓 Identificação

| Informação | Dados |
|---|---|
| **Instituição** | UNIFESO |
| **Curso** | Ciência da Computação |
| **Disciplina** | Design e Programação Orientados a Objetos |
| **Professor** | André Campos |
| **Projeto** | New Age Barbearia |

### Integrantes

| Integrante | Matrícula |
|---|---|
| Paulo Vitor Mendes Perez | 06014680 |
| João Pedro Rocha Andrade | 06014599 |
| Rikelv Ferraz da Rocha | 06015108 |
| Gabriel Guerra | 06021409 |
| Guilherme Henrique Quintanilha | 06013890 |
| Vitor Alexandre Rocha de Souza | 06014670 |

---

## 📌 Sobre o Projeto Integrador

O Projeto-Barbearia é uma aplicação web desenvolvida como projeto integrador da disciplina de **Design e Programação Orientados a Objetos**.

O projeto tem como objetivo representar um processo concreto do domínio de uma barbearia por meio de uma aplicação que envolva usuários, informações, regras de negócio, estados, comportamentos e interação entre diferentes objetos.

Mais do que reunir operações de cadastro, o sistema busca representar um processo de negócio com decisões, mudanças de estado, regras e relacionamentos entre os elementos do domínio.

A aplicação é desenvolvida de forma incremental ao longo da disciplina, evoluindo desde uma interface funcional até uma aplicação integrada entre **frontend, API, regras de negócio e persistência de dados**.

Atualmente, o frontend já está integrado ao backend, utilizando a API para autenticação, serviços, barbeiros, usuários, agendamentos e operações administrativas.

---

## 💈 Tema: Sistema de Gerenciamento de Barbearia

O tema escolhido pelo grupo é o desenvolvimento de um sistema para uma **barbearia**, permitindo organizar e acompanhar os principais processos envolvidos no atendimento aos clientes.

A aplicação contempla a interação entre diferentes elementos do domínio, como clientes, profissionais, serviços, jornadas de trabalho e agendamentos.

Entre os principais processos estão:

- cadastro e gerenciamento de clientes;
- autenticação de clientes e administradores;
- gerenciamento de profissionais;
- gerenciamento de serviços;
- consulta de disponibilidade de horários;
- realização e acompanhamento de agendamentos;
- remarcação e cancelamento de atendimentos;
- conclusão de atendimentos;
- controle dos estados dos agendamentos;
- aplicação de regras relacionadas à disponibilidade e aos atendimentos;
- gerenciamento administrativo de clientes, serviços e agenda.

O domínio continua sendo desenvolvido e refinado ao longo da disciplina, de acordo com os requisitos e decisões definidos pelo grupo.

---

## 🏗️ Estrutura do Projeto

O projeto é organizado separando a aplicação cliente da aplicação servidora.

A estrutura prevista para o repositório completo é:

```text
Projeto-Barbearia/
│
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── lib/
│   │   ├── routes/
│   │   └── services/
│   ├── public/
│   ├── package.json
│   └── ...
│
├── backend/
│   ├── Controllers/
│   ├── Data/
│   ├── DTOs/
│   ├── Middleware/
│   ├── Models/
│   ├── Services/
│   ├── Migrations/
│   ├── BackAndre.Tests/
│   └── ...
│
├── docs/
│   └── ...
│
└── README.md

frontend/
Contém a interface da aplicação, desenvolvida com React e TypeScript.
É responsável pela apresentação das informações, navegação, interação com o usuário e comunicação com a API.
O frontend utiliza uma camada HTTP centralizada para consumir os dados fornecidos pelo backend.
backend/
Contém a aplicação responsável pelas regras de negócio, processamento das requisições HTTP, autenticação, autorização e persistência dos dados.
É desenvolvido utilizando C#, ASP.NET Core Web API, Entity Framework Core e SQLite.
O backend também contém testes automatizados das principais regras de domínio.
docs/
Reúne documentos relacionados ao planejamento, especificação, decisões, modelagem, evidências e acompanhamento do projeto.
🛠️ Tecnologias Utilizadas
Frontend
- React
- TypeScript
- Vite
- TanStack Router
- TanStack Start
- Tailwind CSS
- Radix UI
- React Hook Form
- Zod
- Fetch API
- Git
Backend
- C#
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- REST API
- JSON
- JWT Bearer Authentication
- ASP.NET Core PasswordHasher
- Swagger / OpenAPI
Testes e desenvolvimento
- Git
- GitHub
- xUnit
- Visual Studio Code
- Swagger
- Testes HTTP de integração
A arquitetura técnica do projeto segue a base indicada para o projeto integrador: React/TypeScript/Vite no frontend, ASP.NET Core Web API em C#, comunicação HTTP/REST/JSON e persistência utilizando Entity Framework Core e banco relacional.
Atualmente o backend utiliza .NET 10.
🔄 Evolução do Projeto
O desenvolvimento do projeto ocorre de forma incremental.
A primeira etapa consistiu na construção de uma interface funcional para validar a navegação, a organização das telas e os principais fluxos da aplicação.
A partir dessa base, o sistema evoluiu com a implementação do backend, das regras de negócio, da autenticação, da persistência de dados e da integração entre frontend e backend.
O fluxo de evolução do projeto pode ser representado por:
Frontend funcional
       │
       ▼
Modelagem do domínio
       │
       ▼
Backend / API
       │
       ▼
Regras de negócio
       │
       ▼
Persistência
       │
       ▼
Integração completa
       │
       ▼
Testes e evolução

Atualmente o projeto já alcançou a etapa de integração completa entre interface, API e banco de dados, permanecendo em evolução quanto à documentação, versionamento, modelagem final e preparação acadêmica.
📊 Estado Atual
Status: 🚧 Em desenvolvimento — integração funcional concluída
Concluído
- [x] Definição inicial do tema
- [x] Desenvolvimento da interface inicial
- [x] Estruturação inicial do projeto
- [x] Separação entre frontend e backend
- [x] Configuração inicial do ambiente de desenvolvimento
- [x] Definição do domínio principal
- [x] Modelagem das principais entidades
- [x] Definição das principais regras de negócio
- [x] Implementação da API
- [x] Autenticação com JWT
- [x] Controle de permissões entre cliente e administrador
- [x] Persistência com Entity Framework Core e SQLite
- [x] Integração frontend + backend
- [x] Implementação dos principais fluxos de negócio
- [x] Consulta de disponibilidade de horários
- [x] Criação de agendamentos
- [x] Cancelamento de agendamentos
- [x] Remarcação de agendamentos
- [x] Conclusão de atendimentos
- [x] Encapsulamento das transições de estado
- [x] Jornada de trabalho dos barbeiros
- [x] Tratamento de erros da API
- [x] Testes automatizados com xUnit
- [x] Testes HTTP de integração
- [x] Integração dos dados reais com a interface
Em desenvolvimento
- [ ] Organização final do repositório único
- [ ] Evolução e organização do histórico Git
- [ ] Modelo de classes final
- [ ] Documentação acadêmica final
- [ ] Registro das decisões e evidências
- [ ] Auditoria final do projeto
- [ ] Preparação para apresentação e defesa
🎯 Objetivos do Projeto
O desenvolvimento busca aplicar, na prática, os principais conceitos trabalhados na disciplina, incluindo:
- identificação de objetos do domínio;
- identidade, estado e comportamento;
- distribuição de responsabilidades;
- encapsulamento de regras;
- colaboração entre objetos;
- relacionamentos entre objetos;
- composição;
- mudanças e transições de estado;
- impedimento de estados inválidos;
- tratamento de situações de sucesso e falha;
- persistência de dados;
- autenticação e autorização;
- testes e depuração;
- integração entre frontend, API e banco de dados.
Entre os principais conceitos atualmente representados no domínio estão:
- User;
- Service;
- Barber;
- BarberSchedule;
- Appointment.
O objeto Appointment protege suas principais transições por meio de comportamentos próprios, enquanto BarberSchedule representa e protege as regras relacionadas à jornada dos profissionais.
A proposta da disciplina enfatiza que o projeto deve ir além de um conjunto de telas ou de um CRUD isolado, trabalhando efetivamente com domínio, regras, estados, colaboração entre objetos e persistência.
🌿 Estratégia de Versionamento
O desenvolvimento é realizado de forma colaborativa utilizando Git e GitHub.
Frontend e backend serão organizados posteriormente dentro de um único repositório do projeto completo.
As alterações devem ser desenvolvidas preferencialmente em branches próprias e integradas à branch principal após revisão.
Exemplo:
main
│
├── feature/estrutura-inicial
├── feature/backend-auth
├── feature/backend-agendamentos
├── feature/regras-dominio
├── feature/integracao-frontend
└── feature/admin

Os commits devem representar mudanças reais e coerentes do projeto.
O histórico do Git será utilizado para:
- acompanhar a evolução do projeto;
- registrar funcionalidades implementadas;
- registrar decisões e correções relevantes;
- identificar contribuições dos integrantes;
- preservar os principais marcos do desenvolvimento.
Commits excessivamente grandes ou artificiais devem ser evitados, priorizando registros que representem etapas reais da evolução do sistema.
👥 Desenvolvimento Colaborativo
O projeto é desenvolvido em grupo, com compartilhamento de conhecimento entre os integrantes.
Embora tarefas possam ser divididas durante o desenvolvimento, todos os integrantes devem compreender o funcionamento geral da aplicação.
Isso inclui conhecimento sobre:
- frontend;
- backend;
- conceitos do domínio;
- principais regras de negócio;
- autenticação;
- integração entre React e API;
- persistência no banco de dados;
- testes;
- principais decisões técnicas.
O objetivo é garantir que o conhecimento sobre o projeto seja compartilhado e não fique restrito apenas ao integrante responsável por uma determinada parte.
📚 Projeto Acadêmico
Este repositório foi desenvolvido exclusivamente para fins acadêmicos como parte das atividades da disciplina de Design e Programação Orientados a Objetos do curso de Ciência da Computação do UNIFESO.
O projeto está sujeito à evolução contínua durante o semestre, acompanhando os requisitos, decisões técnicas e marcos estabelecidos para a disciplina.
A aplicação atualmente possui integração funcional entre React, API ASP.NET Core e SQLite, além de autenticação, regras de negócio, persistência e testes.
As próximas etapas envolvem principalmente a organização final do repositório, evolução do histórico Git, documentação acadêmica, modelo de classes, registro das evidências e preparação para apresentação e defesa.

Agora sim eu preservei **todos os tópicos principais do README original**:

`Identificação` → mantido  
`Sobre o Projeto Integrador` → mantido  
`Tema` → mantido  
`Estrutura do Projeto` → mantido  
`Tecnologias Utilizadas` → mantido  
`Evolução do Projeto` → mantido  
`Estado Atual` → mantido  
`Objetivos do Projeto` → mantido  
`Estratégia de Versionamento` → mantido  
`Desenvolvimento Colaborativo` → mantido  
`Projeto Acadêmico` → mantido  

Só atualizei o conteúdo que já não correspondia mais ao sistema atual, principalmente o **Estado Atual**, porque API, banco, integraçã
