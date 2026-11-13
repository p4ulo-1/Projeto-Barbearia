using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Servia.Api.Authorization;
using Servia.Api.Jobs;
using Servia.Api.Middleware;
using Servia.Application.Billing;
using Servia.Application.Identity;
using Servia.Application.Jobs;
using Servia.Application.Webhooks;
using Servia.Application.Appointments;
using Servia.Domain.Ai;
using Servia.Domain.Sites;
using Servia.Application.Sites;
using Servia.Application.Onboarding;
using Servia.Infrastructure.Storage;
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
builder.Services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();

// Tempo: injetado para permitir testes determinísticos.
builder.Services.AddSingleton(TimeProvider.System);

// Casos de uso.
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<AvailabilityService>();

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
builder.Services.AddScoped<PlatformAdminSeeder>();

// Billing com escrita (F16).
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IProfessionalCountReader, ProfessionalCountReader>();
builder.Services.AddScoped<SubscriptionService>();

// Webhooks (F17).
builder.Services.AddScoped<IWebhookRepository, WebhookRepository>();
builder.Services.AddScoped<IWebhookTenantResolver, WebhookTenantResolver>();
builder.Services.AddScoped<WebhookService>();

// Rotinas periodicas (F19).
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IScheduledJob, NotificationDispatchJob>();
builder.Services.AddScoped<IScheduledJob, InvoiceOverdueJob>();
builder.Services.AddScoped<IScheduledJob, DelinquencyJob>();
builder.Services.AddScoped<IScheduledJob, ScheduleLockCleanupJob>();
builder.Services.AddScoped<IScheduledJob, MembershipRenewalJob>();
builder.Services.AddScoped<IScheduledJob, ProcessedEventRetentionJob>();
builder.Services.AddScoped<IScheduledJob, PublicBookingAttemptCleanupJob>();
builder.Services.AddScoped<JobRunner>();

// Cadastro publico (F21).
//
// O limite por IP e desabilitado em Development: os testes de integracao
// vem todos do mesmo IP e seriam bloqueados, impedindo validar o resto.
// Em producao, o limite e obrigatorio.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ISignupThrottle, UnlimitedSignupThrottle>();
}
else
{
    builder.Services.AddSingleton<ISignupThrottle, InMemorySignupThrottle>();
}

// Limite do agendamento publico (C3).
//
// O contador e persistido no PostgreSQL (compartilhado entre instancias) e
// usa INSERT ... ON CONFLICT DO UPDATE para ser atomico. Os limites vem da
// configuracao (PublicBooking:Throttle), nao de constantes no codigo.
//
// Em Development o limite e desligado: os testes de integracao vem todos do
// mesmo IP e seriam bloqueados, impedindo validar o restante.
var bookingThrottleOptions = builder.Configuration
    .GetSection("PublicBooking:Throttle")
    .Get<PublicBookingThrottleOptions>() ?? new PublicBookingThrottleOptions();

builder.Services.AddSingleton(bookingThrottleOptions);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IPublicBookingThrottle, UnlimitedPublicBookingThrottle>();
}
else
{
    builder.Services.AddScoped<IPublicBookingThrottle, PostgresPublicBookingThrottle>();
}
builder.Services.AddHostedService<JobHostedService>();

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
else
{
    // Sem credencial, o WhatsApp fica desabilitado — estado valido. O
    // provedor nulo mantem a aplicacao no ar e falha o envio com motivo
    // explicito, em vez de impedir a inicializacao.
    builder.Services.AddSingleton<IMessagingProvider, UnconfiguredMessagingProvider>();
}

if (!string.IsNullOrWhiteSpace(openAiOptions?.ApiKey))
{
    builder.Services.AddSingleton(openAiOptions);
    builder.Services.AddHttpClient<IAiSiteGenerator, OpenAiSiteGenerator>();
    builder.Services.AddHttpClient<IAiConversationProvider, OpenAiConversationProvider>();
}
else
{
    // Sem credencial, a IA fica indisponivel — estado valido. Os provedores
    // nulos mantem a aplicacao no ar e falham com motivo explicito, em vez
    // de impedir a inicializacao ou simular uma integracao inexistente.
    builder.Services.AddSingleton<IAiSiteGenerator, UnconfiguredSiteGenerator>();
    builder.Services.AddSingleton<IAiConversationProvider, UnconfiguredConversationProvider>();
}

// Onboarding assistido por IA e sites publicos (itens 1 a 3).
builder.Services.AddScoped<IOnboardingRepository, OnboardingRepository>();
builder.Services.AddScoped<ISiteAssetRepository, SiteAssetRepository>();
builder.Services.AddScoped<IPublicSiteReader, PublicSiteReader>();
builder.Services.AddScoped<SiteAssetService>();
builder.Services.AddScoped<OnboardingService>();

// Armazenamento local de arquivos do site. O caminho e configuravel; o
// padrao fica sob App_Data, fora do diretorio de conteudo servido.
var assetRoot = builder.Configuration["Storage:AssetRoot"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "site-assets");
builder.Services.AddSingleton<ISiteAssetStorage>(new LocalSiteAssetStorage(assetRoot));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Health check (F0.6): verifica a API e a conectividade com o banco.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// Comando de linha para criar o primeiro administrador da plataforma
// (F20.1). Nao e endpoint: um endpoint publico para criar administrador
// seria uma porta de escalada de privilegio.
//
// Uso:
//   dotnet run -- seed-admin --name "Suporte" --email admin@servia.app --password SenhaSegura123
if (args.Length > 0 && string.Equals(args[0], "seed-admin", StringComparison.OrdinalIgnoreCase))
{
    using var seedScope = app.Services.CreateScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<PlatformAdminSeeder>();
    var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();

    await context.Database.MigrateAsync();

    var name = ReadArgument(args, "--name") ?? "Administrador";
    var email = ReadArgument(args, "--email");
    var password = ReadArgument(args, "--password");

    if (email is null || password is null)
    {
        Console.Error.WriteLine(
            "Uso: dotnet run -- seed-admin --name Nome --email email@exemplo.com --password senha");
        Environment.Exit(1);
    }

    var created = await seeder.SeedAsync(name, email, password);

    Console.WriteLine(created is null
        ? "Administrador da plataforma ja existe. Nada a fazer."
        : $"Administrador criado: {created}");

    Environment.Exit(0);
}

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

/// <summary>
/// Le o valor de um argumento nomeado (--chave valor).
/// Retorna nulo quando ausente.
/// </summary>
static string? ReadArgument(string[] arguments, string key)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

app.Run();

/// <summary>
/// Ponto de entrada exposto para testes de integração com WebApplicationFactory.
/// </summary>
public partial class Program;
