# Fluxo da aplicação

## Exemplo: barbeiros

```text
GET /api/barbers
      ↓
BarbersController            (Api)
      ↓
BarberService                (Application)
      ↓
BarberRepository             (Infrastructure)
      ↓
AppDbContext                 (Infrastructure)
      ↓
SQLite
```

## Exemplo: cancelar agendamento

```text
PATCH /api/appointments/{id}/cancel
      ↓
AppointmentsController       (Api)
      ↓
AppointmentService           (Application)
      ↓
AppointmentRepository        (Infrastructure)
      ↓
Appointment.Cancel()         (Domain)
      ↓
Repository salva a alteração
```

O Service coordena o caso de uso, mas não substitui as regras que pertencem à própria entidade de domínio.
