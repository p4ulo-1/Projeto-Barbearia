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
| **Integrantes** | **Nome** |
| **1** | Gabriel Guerra |
| **2** | *Preencha seu nome* |
| **3** | *Preencha seu nome* |
| **4** | *Preencha seu nome* |
| **5** | *Preencha seu nome* |
| **6** | *Preencha seu nome* |

---

## 📌 Sobre o Projeto Integrador

O Projeto-Barbearia é uma aplicação web desenvolvida como projeto integrador da disciplina de **Design e Programação Orientados a Objetos**.

O projeto tem como objetivo representar um processo concreto do domínio de uma barbearia por meio de uma aplicação que envolva usuários, informações, regras de negócio, estados, comportamentos e interação entre diferentes objetos.

Mais do que reunir operações de cadastro, o sistema busca representar um processo de negócio com decisões, mudanças de estado, regras e relacionamentos entre os elementos do domínio.

A aplicação é desenvolvida de forma incremental ao longo da disciplina, permitindo que o grupo evolua desde uma primeira versão funcional até a integração entre **interface, API, regras de negócio e persistência de dados**.

---

## 💈 Tema: Sistema de Gerenciamento de Barbearia

O tema escolhido pelo grupo é o desenvolvimento de um sistema para uma **barbearia**, permitindo organizar e acompanhar os principais processos envolvidos no atendimento aos clientes.

A aplicação contempla a interação entre diferentes elementos do domínio, como clientes, profissionais, serviços e agendamentos.

Entre os principais processos previstos estão:

- cadastro e gerenciamento de clientes;
- cadastro e gerenciamento de profissionais;
- gerenciamento de serviços;
- realização e acompanhamento de agendamentos;
- controle dos estados dos agendamentos;
- aplicação de regras relacionadas à disponibilidade e aos atendimentos.

O recorte do domínio será desenvolvido progressivamente durante a disciplina, de acordo com os requisitos e decisões definidos pelo grupo.

---

## 🏗️ Estrutura do Projeto

O repositório está organizado separando a aplicação cliente da aplicação servidora:

```text
Projeto-Barbearia/
│
├── frontend/
│   ├── src/
│   ├── public/
│   ├── package.json
│   └── ...
│
├── backend/
│   ├── Projeto.Api/
│   ├── Projeto.Tests/
│   └── ...
│
├── docs/
│   └── ...
│
└── README.md
```

### `frontend/`

Contém a interface da aplicação, desenvolvida com React e TypeScript.

É responsável pela apresentação das informações, navegação, interação com o usuário e comunicação com a API.

### `backend/`

Contém a aplicação responsável pelas regras de negócio, processamento das requisições HTTP e persistência dos dados.

Será desenvolvido utilizando C# e ASP.NET Core Web API.

### `docs/`

Reúne documentos relacionados ao planejamento, especificação, decisões e acompanhamento do projeto.

---

## 🛠️ Tecnologias Utilizadas

### Frontend

- React
- TypeScript
- Vite
- TanStack Router
- TanStack Start
- Tailwind CSS
- Radix UI
- React Hook Form
- Zod
- Git

### Backend

- C#
- ASP.NET Core Web API
- Entity Framework Core
- Banco de dados relacional
- SQLite
- REST API
- JSON

### Testes e desenvolvimento

- Git
- GitHub
- xUnit ou tecnologia equivalente definida para a disciplina
- Visual Studio Code

A arquitetura técnica do projeto segue a base indicada para o projeto integrador: React/TypeScript/Vite no frontend, ASP.NET Core Web API em C#, comunicação HTTP/REST/JSON e persistência utilizando Entity Framework Core e banco relacional.

---

## 🔄 Evolução do Projeto

O desenvolvimento do projeto ocorre de forma incremental.

A primeira etapa consiste na construção de uma interface funcional que permita validar a navegação, a organização das telas e os principais fluxos da aplicação.

A partir dessa base, o sistema evolui progressivamente com a implementação da API, das regras de negócio, da persistência de dados e da integração entre as diferentes partes da aplicação.

```text
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
```

---

## 📊 Estado Atual

**Status:** 🚧 Em desenvolvimento

### Concluído

- [x] Definição inicial do tema
- [x] Desenvolvimento da interface inicial
- [x] Estruturação do repositório do projeto
- [x] Separação entre frontend e backend
- [x] Configuração inicial do ambiente de desenvolvimento

### Em desenvolvimento

- [ ] Definição detalhada do domínio
- [ ] Modelagem das entidades
- [ ] Definição das regras de negócio
- [ ] Implementação da API
- [ ] Persistência de dados
- [ ] Integração frontend + backend
- [ ] Implementação dos fluxos de negócio
- [ ] Testes
- [ ] Documentação final

---

## 🎯 Objetivos do Projeto

O desenvolvimento busca aplicar, na prática, os principais conceitos trabalhados na disciplina, incluindo:

- identificação de objetos do domínio;
- identidade, estado e comportamento;
- distribuição de responsabilidades;
- encapsulamento de regras;
- colaboração entre objetos;
- relacionamentos entre objetos;
- mudanças de estado;
- tratamento de situações de sucesso e falha;
- persistência de dados;
- testes e depuração;
- integração entre frontend, API e banco de dados.

A proposta da disciplina enfatiza que o projeto deve ir além de um conjunto de telas ou de um CRUD isolado, trabalhando efetivamente com domínio, regras, estados, colaboração entre objetos e persistência.

---

## 🌿 Estratégia de Versionamento

O desenvolvimento é realizado de forma colaborativa utilizando **Git e GitHub**.

As alterações devem ser desenvolvidas preferencialmente em branches próprias e integradas à branch principal após revisão.

Exemplo:

```text
main
│
├── feature/estrutura-inicial
├── feature/backend-clientes
├── feature/backend-agendamentos
└── feature/integracao-frontend
```

O histórico do Git será utilizado para acompanhar a evolução do projeto, registrar decisões e preservar os principais marcos do desenvolvimento.

---

## 👥 Desenvolvimento Colaborativo

O projeto é desenvolvido em grupo, com compartilhamento de conhecimento entre os integrantes.

---

## 📚 Projeto Acadêmico

Este repositório foi desenvolvido exclusivamente para fins acadêmicos como parte das atividades da disciplina de **Design e Programação Orientados a Objetos**.

O projeto está sujeito à evolução contínua durante o semestre, acompanhando os requisitos, decisões técnicas e marcos estabelecidos para a disciplina.
