# 💈 New Age Barbearia

<p align="center">
  <strong>Projeto Integrador — Design e Programação Orientados a Objetos</strong>
</p>

<p align="center">
  Aplicação web completa para gerenciamento de uma barbearia, desenvolvida como projeto acadêmico colaborativo.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Em%20Desenvolvimento-yellow?style=for-the-badge" />
  <img src="https://img.shields.io/badge/Projeto-Acad%C3%AAmico-blue?style=for-the-badge" />
  <img src="https://img.shields.io/badge/React-TypeScript-61DAFB?style=for-the-badge&logo=react&logoColor=white" />
  <img src="https://img.shields.io/badge/C%23-ASP.NET%20Core-512BD4?style=for-the-badge&logo=.net&logoColor=white" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=.net&logoColor=white" />
  <img src="https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
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

### 👥 Integrantes

| Integrante | Matrícula |
|---|---|
| Paulo Vitor Mendes Perez | 06014680 |
| João Pedro Rocha Andrade | 06014599 |
| Rikelv Ferraz da Rocha | 06015108 |
| Gabriel Guerra | 06021409 |
| Guilherme Henrique Quintanilha | 06013890 |
| Vitor Alexandre Rocha de Souza | 06014670 |

---

## 📌 Sobre o Projeto

O **New Age Barbearia** é uma aplicação web desenvolvida como Projeto Integrador da disciplina de **Design e Programação Orientados a Objetos**.

O sistema representa um processo real de atendimento em uma barbearia, permitindo que clientes consultem serviços e profissionais, criem agendamentos, acompanhem seus horários e realizem operações como cancelamento e remarcação.

Além das funcionalidades voltadas ao cliente, o sistema possui uma área administrativa responsável pelo acompanhamento dos usuários, serviços e agendamentos.

O projeto foi desenvolvido buscando aplicar conceitos de orientação a objetos, separação de responsabilidades, regras de negócio, persistência de dados, autenticação, testes e integração entre frontend e backend.

---

# 💈 Funcionalidades

## 👤 Cliente

O usuário pode:

- criar uma conta;
- realizar login;
- manter sua sessão autenticada;
- consultar seu perfil;
- atualizar informações pessoais;
- alterar sua senha;
- excluir sua conta;
- visualizar serviços disponíveis;
- visualizar barbeiros;
- consultar horários disponíveis;
- criar um agendamento;
- visualizar seus próprios agendamentos;
- remarcar um atendimento;
- cancelar um atendimento.

---

## 🛡️ Administrador

O administrador pode:

- realizar login com permissões administrativas;
- consultar clientes cadastrados;
- consultar todos os agendamentos;
- editar serviços;
- desativar serviços;
- concluir atendimentos;
- excluir agendamentos;
- acompanhar a agenda da barbearia.

As permissões administrativas são protegidas no backend por autenticação JWT e roles.

---

# 🧠 Domínio da Aplicação

O sistema atualmente trabalha com os seguintes conceitos principais de domínio:

### `User`

Representa um usuário do sistema.

Responsável por informações como:

- nome;
- e-mail;
- telefone;
- perfil de acesso (`client` ou `admin`).

---

### `Service`

Representa um serviço oferecido pela barbearia.

Possui informações como:

- nome;
- descrição;
- preço;
- duração;
- estado ativo/inativo.

A exclusão de um serviço é lógica através de `IsActive`, preservando o histórico dos agendamentos anteriores.

---

### `Barber`

Representa um profissional da barbearia.

Possui informações como:

- nome;
- cargo;
- descrição;
- jornada de trabalho.

---

### `BarberSchedule`

Representa a jornada de trabalho pertencente ao barbeiro.

É responsável por regras como:

- identificar os dias em que o barbeiro trabalha;
- verificar início e fim do expediente;
- verificar se determinado atendimento cabe dentro da jornada.

Exemplos de comportamento:

```csharp
schedule.WorksOn(date);

schedule.FitsWithinSchedule(time, duration);
