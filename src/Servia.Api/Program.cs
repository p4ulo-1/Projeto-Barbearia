using Microsoft.EntityFrameworkCore;
using Servia.Api.Middleware;
using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Persistência. Em desenvolvimento usa SQLite; em produção, PostgreSQL.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=servia.db";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Repositórios: a aplicação depende do contrato, não da implementação.
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();

// Tempo: injetado para permitir testes determinísticos.
builder.Services.AddSingleton(TimeProvider.System);

// Casos de uso.
builder.Services.AddScoped<AppointmentService>();

var app = builder.Build();

// O tratamento de exceções é o PRIMEIRO middleware do pipeline.
// Assim, qualquer exceção lançada adiante — controllers, serviços, domínio —
// é capturada e traduzida em resposta HTTP consistente.
app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Cria o schema em desenvolvimento. Em produção, o schema é aplicado
    // por migrations, nunca por EnsureCreated.
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

/// <summary>
/// Ponto de entrada exposto para testes de integração com WebApplicationFactory.
/// </summary>
public partial class Program;
