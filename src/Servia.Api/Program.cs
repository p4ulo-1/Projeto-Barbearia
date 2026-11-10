using Microsoft.EntityFrameworkCore;
using Servia.Api.Middleware;
using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Persistência (F0.7): o provedor é escolhido pela configuração, não pelo
// código. Em desenvolvimento usa SQLite; em produção, PostgreSQL.
// A chave "Database:Provider" permite trocar sem recompilar.
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=servia.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// Contexto de tenant: singleton, pois o estado vive no AsyncLocal.
// Alimenta o filtro global do EF Core (ADR-016).
builder.Services.AddSingleton<ITenantContext, TenantContext>();

// Repositórios: a aplicação depende do contrato, não da implementação.
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();

// Tempo: injetado para permitir testes determinísticos.
builder.Services.AddSingleton(TimeProvider.System);

// Casos de uso.
builder.Services.AddScoped<AppointmentService>();

// Health check (F0.6): verifica a API e a conectividade com o banco.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// O tratamento de exceções é o PRIMEIRO middleware do pipeline.
// Assim, qualquer exceção lançada adiante — controllers, serviços, domínio —
// é capturada e traduzida em resposta HTTP consistente.
app.UseExceptionHandling();

// Resolve o tenant no servidor, antes de qualquer acesso a dados.
app.UseTenantResolution();

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

// Endpoint de saúde: responde 200 quando API e banco estão operacionais.
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Ponto de entrada exposto para testes de integração com WebApplicationFactory.
/// </summary>
public partial class Program;
