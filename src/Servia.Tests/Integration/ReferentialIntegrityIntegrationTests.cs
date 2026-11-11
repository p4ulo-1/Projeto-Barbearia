using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Contracts;
using Servia.Tests.Api;
using Servia.Api.Controllers;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Loyalty;
using Servia.Domain.Memberships;

namespace Servia.Tests.Integration;

/// <summary>
/// Etapa 8 — integridade referencial e concorrência pela API real.
///
/// Verifica que o PostgreSQL rejeita associações entre tenants e que
/// requisições simultâneas não geram dupla reserva.
/// </summary>
public class ReferentialIntegrityIntegrationTests : MultiTenantIntegrationTestBase
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    public ReferentialIntegrityIntegrationTests(IsolatedApiFactory factory)
        : base(factory)
    {
    }

    // ---------- Integridade referencial ----------

    [Fact]
    public async Task Banco_Rejeita_Assinatura_Com_Cliente_De_Outro_Tenant()
    {
        var customerB = await WithTenantAsync(TenantB, async context =>
        {
            var customer = Customer.Create(TenantB, "Cliente B", "21999998888");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            return customer.Id;
        });

        var planA = await WithTenantAsync(TenantA, async context =>
        {
            var plan = MembershipPlan.Create(TenantA, "Clube A", Money.From(99m));
            context.MembershipPlans.Add(plan);
            await context.SaveChangesAsync();

            return plan.Id;
        });

        // Tenant A tenta associar um cliente do tenant B.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            WithTenantAsync(TenantA, async context =>
            {
                var membership = Membership.Subscribe(
                    TenantA, customerB, planA, Today, Today.AddDays(30));

                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                return 0;
            }));

        Assert.Contains("23503", exception.InnerException?.Message ?? string.Empty);
    }

    [Fact]
    public async Task Banco_Rejeita_Conta_De_Fidelidade_Com_Cliente_De_Outro_Tenant()
    {
        var customerB = await WithTenantAsync(TenantB, async context =>
        {
            var customer = Customer.Create(TenantB, "Cliente B", "21999997777");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            return customer.Id;
        });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            WithTenantAsync(TenantA, async context =>
            {
                var account = LoyaltyAccount.Open(TenantA, customerB);
                context.LoyaltyAccounts.Add(account);
                await context.SaveChangesAsync();

                return 0;
            }));
    }

    [Fact]
    public async Task Banco_Aceita_Associacao_Dentro_Do_Mesmo_Tenant()
    {
        var (customerId, planId) = await WithTenantAsync(TenantA, async context =>
        {
            var customer = Customer.Create(TenantA, "Cliente A", "21999996666");
            var plan = MembershipPlan.Create(TenantA, "Clube A", Money.From(99m));

            context.Customers.Add(customer);
            context.MembershipPlans.Add(plan);
            await context.SaveChangesAsync();

            return (customer.Id, plan.Id);
        });

        var membershipId = await WithTenantAsync(TenantA, async context =>
        {
            var membership = Membership.Subscribe(
                TenantA, customerId, planId, Today, Today.AddDays(30));

            context.Memberships.Add(membership);
            await context.SaveChangesAsync();

            return membership.Id;
        });

        Assert.NotEqual(Guid.Empty, membershipId);
    }

    // ---------- Concorrência pela API ----------

    [Fact]
    public async Task Requisicoes_Simultaneas_Pela_API_Aceitam_Apenas_Um_Agendamento()
    {
        var token = await SignInAsTenantAAsync();

        // Cria as referencias necessarias.
        var customerResponse = await SendAsync(HttpMethod.Post, "/api/v1/customers", token,
            new CreateCustomerRequest("Cliente Concorrencia", "21999995555"));

        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        var serviceResponse = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services", token,
            new CreateServiceRequest("Servico Concorrencia", 50m, 30));

        var service = await serviceResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        var professionalResponse = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals", token,
            new CreateProfessionalRequest(
                "Profissional Concorrencia",
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                 DayOfWeek.Thursday, DayOfWeek.Friday],
                new TimeOnly(9, 0), new TimeOnly(18, 0)));

        var professional = await professionalResponse.Content
            .ReadFromJsonAsync<ProfessionalResponse>();

        // Data futura em dia util.
        var date = new DateOnly(2026, 11, 17);

        var request = new CreateAppointmentRequest(
            customer!.Id, service!.Id, professional!.Id, date, new TimeOnly(10, 0), 30);

        // Cinco requisicoes simultaneas no mesmo horario.
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => SendAsync(HttpMethod.Post, "/api/v1/agenda/appointments", token, request))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        var created = responses.Count(response => response.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);

        // Exatamente uma vence.
        Assert.Equal(1, created);
        Assert.Equal(4, conflicts);
    }

    // ---------- Acesso administrativo ----------

    [Fact]
    public async Task Proprietario_De_Tenant_Nao_Provisiona_Outro_Estabelecimento()
    {
        var token = await SignInAsTenantAAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/platform/tenants", token,
            new ProvisionTenantApiRequest(
                $"tentativa-{Guid.NewGuid():N}"[..20],
                "Invasor", Domain.Tenancy.SegmentType.Barbershop,
                "12345678000190", "ESSENCIAL",
                "Invasor", "invasor@exemplo.com", Password));

        // Apenas PlatformAdmin provisiona.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Provisionamento_Sem_Token_Retorna_401()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/platform/tenants",
            new ProvisionTenantApiRequest(
                "sem-token", "Teste", Domain.Tenancy.SegmentType.Barbershop,
                "12345678000190", "ESSENCIAL",
                "Teste", "teste@exemplo.com", Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
