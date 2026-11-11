using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Servia.Api.Authorization;
using Servia.Api.Middleware;
using Servia.Application.Billing;
using Servia.Application.Identity;
using Servia.Application.Webhooks;
using Servia.Application.Appointments;
using Servia.Domain.Ai;
using Servia.Domain.Appointments;
using Servia.Domain.Billing;
using Servia.Domain.Identity;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Integrations;
using Servia.Infrastructure.Provisioning;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Persistência (F0.7): o provedor é escolhido pela configuração, não pelo
// código. Em desenvolvimento usa SQLite; em produção, PostgreSQL.
// PostgreSQL 17 e o unico provider suportado (Etapa 1).
// Desenvolvimento, testes e producao usam o mesmo banco de referencia:
// testar em outro provider mascararia violacoes de FK, diferencas de
// traducao de consultas e semantica de texto.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection e obrigatoria. "
        + "Configure via variavel de ambiente ConnectionStrings__DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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

// Valida a configuracao JWT na inicializacao, nao no primeiro uso.
// Sem isso, a aplicacao sobe e toda requisicao falha com 500 — falha
// silenciosa que so aparece em producao.
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey e obrigatoria em producao e deve ter ao menos 32 caracteres. "
            + "Configure via variavel de ambiente Jwt__SigningKey.");
    }
}

builder.Services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<TenantProvisioner>();

// Billing com escrita (F16).
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IProfessionalCountReader, ProfessionalCountReader>();
builder.Services.AddScoped<SubscriptionService>();

// Webhooks (F17).
builder.Services.AddScoped<IWebhookRepository, WebhookRepository>();
builder.Services.AddScoped<IWebhookTenantResolver, WebhookTenantResolver>();
builder.Services.AddScoped<WebhookService>();

// A validacao de assinatura depende dos segredos configurados. Sem eles,
// rejeita tudo — falhar fechado em fronteira de confianca.
builder.Services.AddSingleton<IWebhookSignatureValidator>(provider =>
    new WebhookSignatureValidator(
        provider.GetService<MercadoPagoOptions>() ?? new MercadoPagoOptions(),
        provider.GetService<MetaWhatsAppOptions>() ?? new MetaWhatsAppOptions()));

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

builder.Services.AddAuthorization(options =>
{
    options.AddPermissionPolicies();

    // Administrador da plataforma: papel distinto das permissoes de tenant.
    options.AddPolicy(
        Servia.Api.Controllers.RequirePlatformAdminAttribute.PolicyName,
        policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(JwtTokenIssuer.RoleClaim, nameof(Role.PlatformAdmin)));
});

// ---------- Integracoes (F12) ----------
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

// Os provedores so sao registrados quando configurados. Sem credencial,
// a aplicacao sobe e o endpoint responde 503 ao tentar usar o recurso,
// em vez de falhar na inicializacao inteira.
var mercadoPagoOptions = builder.Configuration.GetSection("MercadoPago").Get<MercadoPagoOptions>();
var metaOptions = builder.Configuration.GetSection("MetaWhatsApp").Get<MetaWhatsAppOptions>();
var openAiOptions = builder.Configuration.GetSection("OpenAi").Get<OpenAiOptions>();

if (!string.IsNullOrWhiteSpace(mercadoPagoOptions?.AccessToken))
{
    builder.Services.AddSingleton(mercadoPagoOptions);
    builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoGateway>();
}

if (!string.IsNullOrWhiteSpace(metaOptions?.AccessToken))
{
    builder.Services.AddSingleton(metaOptions);
    builder.Services.AddHttpClient<IMessagingProvider, MetaWhatsAppProvider>();
}

if (!string.IsNullOrWhiteSpace(openAiOptions?.ApiKey))
{
    builder.Services.AddSingleton(openAiOptions);
    builder.Services.AddHttpClient<IAiSiteGenerator, OpenAiSiteGenerator>();
}
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

    // F11.2: em PostgreSQL, o schema e aplicado por migrations, nunca por
    // EnsureCreated. EnsureCreated nao versiona nada: a primeira alteracao
    // de modelo quebraria o banco sem caminho de volta.
    //
    // Em SQLite (testes), usa EnsureCreated: o banco e efemero e o snapshot
    // de migrations e gerado para PostgreSQL. Aplicar migrations de um
    // provider em outro faz o EF Core 10 detectar divergencia falsa.
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await context.Database.MigrateAsync();

    // F11.3: garante os planos comerciais da plataforma.
    await PlanSeeder.SeedAsync(context);
}

// F15.3: em producao, exige HTTPS. O redirecionamento e feito pelo proxy
// reverso (que termina o TLS); aqui garantimos que o esquema seja https.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapControllers();

// Endpoints de saúde (F15.4):
//   /health       — estado completo, usado por monitoramento
//   /health/live  — o processo está vivo (não verifica dependências)
//   /health/ready — pronto para receber tráfego (verifica o banco)
//
// A separação importa: um orquestrador reinicia o processo quando
// /health/live falha, mas só remove do balanceador quando /health/ready
// falha. Reiniciar por causa do banco não resolve nada.
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

app.Run();

/// <summary>
/// Ponto de entrada exposto para testes de integração com WebApplicationFactory.
/// </summary>
public partial class Program;
