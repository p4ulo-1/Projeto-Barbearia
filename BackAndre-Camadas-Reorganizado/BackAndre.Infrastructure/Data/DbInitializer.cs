using BackAndre.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();

        if (!await db.Users.AnyAsync())
        {
            var admin = new User
            {
                Id = "u-admin",
                Name = "Rafael Nunes",
                Email = "admin@newagebarber.com.br",
                Phone = "(21) 99999-0001",
                Role = User.AdminRole
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "newage123");

            var client = new User
            {
                Id = "u-demo",
                Name = "Bruno Salgado",
                Email = "cliente@exemplo.com",
                Phone = "(21) 98888-1234",
                Role = User.ClientRole
            };
            client.PasswordHash = passwordHasher.HashPassword(client, "cliente123");

            db.Users.AddRange(admin, client);
        }

        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(
                new Service
                {
                    Id = "corte-classico",
                    Name = "Corte Clássico",
                    Description = "Tesoura e máquina, finalização com pomada e toalha quente.",
                    Price = 35,
                    Duration = 45
                },
                new Service
                {
                    Id = "corte-navalhado",
                    Name = "Corte Navalhado",
                    Description = "Fade fechado com acabamento na navalha e contorno milimétrico.",
                    Price = 40,
                    Duration = 60,
                    Highlight = true
                },
                new Service
                {
                    Id = "barba-terapia",
                    Name = "Barba Terapia",
                    Description = "Toalha quente, óleo essencial, navalha e balm calmante.",
                    Price = 35,
                    Duration = 40
                },
                new Service
                {
                    Id = "combo-premium",
                    Name = "Combo Premium",
                    Description = "Corte navalhado + barba terapia, com dose de whisky por conta da casa.",
                    Price = 70,
                    Duration = 90,
                    Highlight = true
                },
                new Service
                {
                    Id = "pigmentacao",
                    Name = "Pigmentação de Barba",
                    Description = "Correção de falhas com pigmento à prova d'água.",
                    Price = 30,
                    Duration = 30
                },
                new Service
                {
                    Id = "kids",
                    Name = "Corte Infantil",
                    Description = "Para os cavalheiros de até 10 anos, com paciência inclusa.",
                    Price = 25,
                    Duration = 30
                });
        }

        if (!await db.Barbers.AnyAsync())
        {
            db.Barbers.AddRange(
                new Barber(
                    "rafael",
                    "Rafael Nunes",
                    "Master barber & sócio",
                    "18 anos de ofício. Especialista em cortes clássicos e barba desenhada.",
                    new BarberSchedule([2, 3, 4, 5, 6], new TimeOnly(9, 0), new TimeOnly(20, 0))),
                new Barber(
                    "tiago",
                    "Tiago Marques",
                    "Barbeiro sênior",
                    "Referência em fades e degradês navalhados de alta precisão.",
                    new BarberSchedule([2, 3, 4, 5, 6], new TimeOnly(10, 0), new TimeOnly(20, 0))),
                new Barber(
                    "helena",
                    "Helena Duarte",
                    "Barbeira & colorista",
                    "Cortes texturizados, pigmentação e cuidados com cabelos cacheados.",
                    new BarberSchedule([3, 4, 5, 6], new TimeOnly(9, 0), new TimeOnly(18, 0))));
        }

        await db.SaveChangesAsync();

        if (!await db.Appointments.AnyAsync())
        {
            var rafaelDay = FindWorkingDay([2, 3, 4, 5, 6], 1, 1);
            var tiagoPastDay = FindWorkingDay([2, 3, 4, 5, 6], -1, -1);
            var helenaDay = FindWorkingDay([3, 4, 5, 6], 2, 1);

            var first = new Appointment(
                "a-1", "u-demo", "combo-premium", "rafael", rafaelDay, new TimeOnly(15, 0));
            var completed = new Appointment(
                "a-2", "u-demo", "barba-terapia", "tiago", tiagoPastDay, new TimeOnly(11, 30));
            completed.Complete(DateTime.Now);
            var third = new Appointment(
                "a-3", "u-demo", "corte-navalhado", "helena", helenaDay, new TimeOnly(10, 0));

            db.Appointments.AddRange(first, completed, third);

            await db.SaveChangesAsync();
        }
    }

    private static DateOnly FindWorkingDay(int[] workDays, int startOffset, int direction)
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(startOffset);
        while (!workDays.Contains((int)date.DayOfWeek))
        {
            date = date.AddDays(direction);
        }

        return date;
    }
}
