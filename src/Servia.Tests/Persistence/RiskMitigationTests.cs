using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Provisioning;
using Servia.Infrastructure.Tenancy;

using Servia.Tests.Common;

namespace Servia.Tests.Persistence;

/// <summary>
/// Etapa 7 da auditoria — riscos R1, R3 e R5.
///
/// R1: <c>Guid.Empty</c> como sentinela de ausencia de tenant.
/// R3: sensibilidade de pesquisas textuais.
/// R5: colisao de slug no provisionamento concorrente.
/// </summary>
public class RiskMitigationTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        await using var context = _database.CreateContext(new TenantContext());
        await PlanSeeder.SeedAsync(context);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    // ---------- R1: Guid.Empty nao e sentinela ----------

    [Fact]
    public async Task Sem_Tenant_Nao_Retorna_Dados_Com_TenantId_Vazio()
    {
        // Cria um registro com TenantId = Guid.Empty, como o PlatformAdmin.
        await using (var context = _database.CreateContext(new TenantContext()))
        {
            var user = User.CreatePlatformAdmin(
                "Suporte", "admin@servia.app", "hash", Now);

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        // Sem tenant resolvido, o filtro compara com null: nada e visivel.
        await using (var context = _database.CreateContext(new TenantContext()))
        {
            var visible = await context.Users.ToListAsync();

            Assert.Empty(visible);
        }
    }

    [Fact]
    public async Task Tenant_Real_Nao_Ve_Registro_Com_TenantId_Vazio()
    {
        await using (var context = _database.CreateContext(new TenantContext()))
        {
            context.Users.Add(User.CreatePlatformAdmin(
                "Suporte", "admin@servia.app", "hash", Now));

            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            // O tenant A nao ve o administrador da plataforma.
            var visible = await context.Users.ToListAsync();

            Assert.Empty(visible);
        }
    }

    // ---------- R3: busca case-insensitive ----------

    [Fact]
    public async Task Busca_Com_ILIKE_Ignora_Caixa()
    {
        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            context.Customers.Add(Customer.Create(
                TenantA, "João Silva", "21999998888", now: Now));

            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            // Busca por minusculas encontra o nome com maiusculas.
            var found = await context.Customers
                .Where(customer => EF.Functions.ILike(customer.Name, "%joão%"))
                .ToListAsync();

            Assert.Single(found);
        }
    }

    [Fact]
    public async Task Busca_Com_ILIKE_Encontra_Por_Telefone()
    {
        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            context.Customers.Add(Customer.Create(
                TenantA, "Maria", "21988887777", now: Now));

            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var found = await context.Customers
                .Where(customer => EF.Functions.ILike(customer.Phone, "%98888%"))
                .ToListAsync();

            Assert.Single(found);
        }
    }

    // ---------- R5: colisao de slug ----------

    [Fact]
    public async Task Provisionamento_Concorrente_Com_Mesmo_Slug_Retorna_Erro_De_Negocio()
    {
        var slug = $"corrida-{Guid.NewGuid():N}"[..20];

        // Duas requisicoes simultaneas com o mesmo slug.
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(async () =>
            {
                await using var context = _database.CreateContext(new TenantContext());
                var provisioner = new TenantProvisioner(context, new FixedTimeProvider(Now));

                try
                {
                    await provisioner.ProvisionAsync(new ProvisionTenantRequest(
                        slug, "Barbearia", SegmentType.Barbershop,
                        "12345678000190", "ESSENCIAL",
                        "Ana", $"ana-{Guid.NewGuid():N}@exemplo.com", "hash"));

                    return "ok";
                }
                catch (DomainRuleException)
                {
                    return "conflito";
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Uma vence; a outra recebe erro de negocio, nao 500.
        Assert.Contains("ok", results);
        Assert.Contains("conflito", results);

        // E apenas um tenant foi criado com o slug.
        await using var check = _database.CreateContext(new TenantContext());
        var count = await check.Tenants.CountAsync(tenant => tenant.Slug == slug);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Provisionamento_Sequencial_Com_Slug_Duplicado_Lanca_Excecao_De_Negocio()
    {
        var slug = $"sequencial-{Guid.NewGuid():N}"[..20];

        await using (var context = _database.CreateContext(new TenantContext()))
        {
            var provisioner = new TenantProvisioner(context, new FixedTimeProvider(Now));

            await provisioner.ProvisionAsync(new ProvisionTenantRequest(
                slug, "Barbearia", SegmentType.Barbershop,
                "12345678000190", "ESSENCIAL",
                "Ana", "ana1@exemplo.com", "hash"));
        }

        await using (var context = _database.CreateContext(new TenantContext()))
        {
            var provisioner = new TenantProvisioner(context, new FixedTimeProvider(Now));

            await Assert.ThrowsAsync<DomainRuleException>(() =>
                provisioner.ProvisionAsync(new ProvisionTenantRequest(
                    slug, "Outra", SegmentType.Barbershop,
                    "98765432000110", "ESSENCIAL",
                    "Bruno", "bruno@exemplo.com", "hash")));
        }
    }
}

