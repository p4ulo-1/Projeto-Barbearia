using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Servia.Api.Authorization;
using Servia.Api.Middleware;
using Servia.Application.Identity;
using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Identity;
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

// ---------- Autenticacao (F10) ----------
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

// Em desenvolvimento, gera uma chave efemera para nao exigir configuracao
// local. Em producao, a chave e obrigatoria e a aplicacao falha sem ela.
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    jwtOptions.SigningKey = Convert.ToBase64String(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
}

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<AuthenticationService>();

// Preserva os nomes de claim originais (tenant_id, role) em vez de
// mapea-los para as URIs longas do .NET. O handler de permissao depende disso.
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options => options.AddPermissionPolicies());
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Health check (F0.6): verifica a API e a conectividade com o banco.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// O tratamento de exceções é o PRIMEIRO middleware do pipeline.
// Assim, qualquer exceção lançada adiante — controllers, serviços, domínio —
// é capturada e traduzida em resposta HTTP consistente.
app.UseExceptionHandling();

// Autenticacao antes da resolucao de tenant: o tenant vem da claim do token.
app.UseAuthentication();
app.UseAuthorization();

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
