using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Application.Jobs;
using Servia.Domain.Appointments;
using Servia.Domain.Billing;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Sites;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Integrations;
using Servia.Infrastructure.Persistence;
using Servia.Tests.Api;

namespace Servia.Tests.Integration;

public sealed class SubscriptionCheckoutIntegrationTests(CheckoutApiFactory factory)
    : IClassFixture<CheckoutApiFactory>
{
    private const string Checkout = "/api/v1/billing/subscription/checkout";
    private const string Password = "SenhaCheckout123!";
    private const string Billing = "/api/v1/billing/subscription";

    [Fact]
    public async Task Trial_Autorizado_Expira_No_Instante_E_Renovacao_Restaura_Acesso_Apos_48h()
    {
        using var tenant = await SignUpAsync(legacy: false);
        using var other = await SignUpAsync();
        using var receptionist = await SignInReceptionistAsync(tenant.Id);
        var gateway = factory.Stub;
        gateway.Reset();
        gateway.Now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        try
        {
            var request = new StartCheckoutRequest(Guid.NewGuid(), "PRO");
            var service = new CreateServiceRequest("Serviço E5", 35, 30);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", service)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await tenant.Client.PostAsJsonAsync(Checkout, request)).StatusCode);
            gateway.TrialDays = 15;
            Assert.Equal(HttpStatusCode.BadRequest, (await tenant.Client.PostAsJsonAsync(Checkout,
                request with { CardToken = "opaque-test-token" })).StatusCode);
            Assert.Equal(0, gateway.Posts);
            gateway.TrialDays = 14;
            gateway.CreditOnly = false;
            Assert.Equal(HttpStatusCode.BadRequest, (await tenant.Client.PostAsJsonAsync(Checkout,
                request with { CardToken = "opaque-test-token" })).StatusCode);
            Assert.Equal(0, gateway.Posts);
            gateway.CreditOnly = true;
            var issued = await StartAsync(tenant.Client, request with { CardToken = "opaque-test-token" }, "AwaitingExternalConfirmation");
            var trial = await VerifyAsync(tenant.Client, request.CheckoutKey);
            Assert.Equal("Trialing", trial.State);
            Assert.Equal(gateway.Now, trial.TrialStartsAt);
            var firstDue = trial.TrialEndsAt!.Value;
            Assert.Equal(trial.TrialStartsAt!.Value.AddDays(14), firstDue);
            Assert.Equal(issued.AttemptId, trial.AttemptId);
            Assert.Equal(1, gateway.Posts);
            Assert.Equal("ESSENCIAL", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!.PlanCode);
            Assert.Equal(HttpStatusCode.Created, (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", service)).StatusCode);
            gateway.Now = firstDue.AddMilliseconds(-1);
            Assert.Equal(trial.TrialEndsAt, (await VerifyAsync(tenant.Client, request.CheckoutKey)).TrialEndsAt);
            gateway.Now = firstDue;
            gateway.CycleDue = firstDue;
            gateway.Rejected = true;
            Assert.Equal("FirstPaymentRejected", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", service)).StatusCode);
            gateway.Rejected = false;
            gateway.Approved = true;
            Assert.Equal("Active", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
            var current = (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!;
            Assert.Equal("PRO", current.PlanCode);
            var due = current.NextChargeAtUtc!.Value;

            gateway.Now = due;
            gateway.CycleDue = due;
            gateway.NewPayment();
            gateway.Approved = false;
            gateway.Rejected = true;
            var failed = await VerifyCycleAsync(tenant.Client);
            Assert.Equal("PaymentRejected", failed.State);
            Assert.Equal(due, failed.PastDueSince);
            Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Client.PostAsJsonAsync("/api/v1/agenda/appointments",
                new CreateAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2027, 1, 4),
                    new TimeOnly(9, 0)))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, await AttemptPublicBookingAsync(tenant.Id));
            gateway.Now = due.AddHours(48).AddMilliseconds(-1);
            Assert.Equal(HttpStatusCode.Created, (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", service)).StatusCode);
            gateway.Now = due.AddHours(48);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", service)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await tenant.Client.GetAsync("/api/v1/catalog/services")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await receptionist.GetAsync("/api/v1/catalog/services")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await other.Client.GetAsync("/api/v1/catalog/services")).StatusCode);
            gateway.Rejected = false;
            gateway.Approved = true;
            Assert.Equal("Renewed", (await VerifyCycleAsync(tenant.Client)).State);
            Assert.Equal("Current", (await VerifyCycleAsync(tenant.Client)).State);
            Assert.Equal(HttpStatusCode.Created, (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", service)).StatusCode);
            await WithTenantAsync(tenant.Id, async db =>
            {
                var invoices = await db.PlatformInvoices.OrderBy(invoice => invoice.RecurringDueAtUtc).ToListAsync();
                Assert.Equal(2, invoices.Count);
                Assert.All(invoices, invoice => Assert.True(invoice.IsPaid));
                Assert.Equal(firstDue, invoices[0].RecurringFailureAtUtc);
                Assert.Equal(due, invoices[1].RecurringFailureAtUtc);
                Assert.Equal(199m, invoices[1].Total.Amount);
            });
        }
        finally { gateway.Now = null; }
    }

    [Fact]
    public async Task Cancelamento_Ambiguo_Bloqueia_Trial_Sem_Reenviar_E_Nao_Antecipa_Outra_Contratacao()
    {
        using var tenant = await SignUpAsync(legacy: false);
        var gateway = factory.Stub;
        gateway.Reset();
        var request = new StartCheckoutRequest(Guid.NewGuid(), "PRO", "opaque-test-token");
        await StartAsync(tenant.Client, request, "AwaitingExternalConfirmation");
        var trial = await VerifyAsync(tenant.Client, request.CheckoutKey);
        Assert.Equal("Trialing", trial.State);
        gateway.CancellationTimeout = true;
        Assert.Equal(HttpStatusCode.Accepted, (await tenant.Client.PostAsync(Billing + "/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await tenant.Client.PostAsync(Billing + "/cancel", null)).StatusCode);
        Assert.Equal(1, gateway.CancellationPosts);
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services",
            new CreateServiceRequest("Bloqueado", 10, 30))).StatusCode);
        gateway.Cancelled = true;
        Assert.Equal("Cancelled", (await VerifyCycleAsync(tenant.Client)).State);
        Assert.Equal("WaitingForTrialEnd", (await StartAsync(tenant.Client,
            new StartCheckoutRequest(Guid.NewGuid(), "PRO"), "WaitingForTrialEnd")).State);
        Assert.Equal(1, gateway.Posts);
    }

    [Fact]
    public async Task Downgrade_Revalida_Capacidade_Upgrade_Aguarda_E_Cancelamento_Preserva_Periodo_Pago()
    {
        using var tenant = await SignUpAsync();
        await AgeTrialAsync(tenant.Id);
        var gateway = factory.Stub;
        gateway.Reset();
        gateway.Now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        try
        {
            var checkout = new StartCheckoutRequest(Guid.NewGuid(), "PRO");
            await StartAsync(tenant.Client, checkout, "AwaitingAuthorization");
            gateway.Approved = gateway.Authorized = true;
            Assert.Equal("Active", (await VerifyAsync(tenant.Client, checkout.CheckoutKey)).State);
            var paid = (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!;
            var next = paid.NextChargeAtUtc!.Value;
            Guid extra = default;
            await WithTenantAsync(tenant.Id, async db =>
            {
                var service = Service.Create(tenant.Id, "Corte", Money.From(30m), 30);
                db.Services.Add(service);
                for (var index = 0; index < 4; index++)
                {
                    var person = Professional.Create(tenant.Id, $"Equipe {index}",
                        WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(17, 0)));
                    db.Professionals.Add(person);
                    db.ProfessionalServices.Add(ProfessionalService.Create(tenant.Id, person.Id, service.Id));
                    extra = person.Id;
                }
                await db.SaveChangesAsync();
            });
            var plan = new SubscribePlanRequest("ESSENCIAL");
            Assert.Equal(HttpStatusCode.BadRequest,
                (await tenant.Client.PostAsJsonAsync(Billing + "/plan-change", plan)).StatusCode);
            await SetProfessionalAsync(tenant.Id, extra, false);
            var scheduled = await tenant.Client.PostAsJsonAsync(Billing + "/plan-change", plan);
            Assert.Equal(HttpStatusCode.Accepted, scheduled.StatusCode);
            Assert.Equal(next, (await scheduled.Content.ReadFromJsonAsync<PlanChangeRequestResponse>())!.EffectiveAt);
            Assert.Equal("Current", (await VerifyCycleAsync(tenant.Client)).State);
            Assert.Equal("PRO", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!.PlanCode);

            await SetProfessionalAsync(tenant.Id, extra, true);
            gateway.Now = next;
            gateway.CycleDue = next;
            gateway.NewPayment();
            gateway.NewRecurringAmount = 99m;
            Assert.Equal("CapacityExceeded", (await VerifyCycleAsync(tenant.Client)).State);
            Assert.Equal("PRO", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!.PlanCode);
            await SetProfessionalAsync(tenant.Id, extra, false);
            var renewed = await VerifyCycleAsync(tenant.Client);
            Assert.Equal("Renewed", renewed.State);
            Assert.Equal("ESSENCIAL", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!.PlanCode);

            var upgrade = await tenant.Client.PostAsJsonAsync(Billing + "/plan-change", new SubscribePlanRequest("PRO"));
            Assert.Equal(HttpStatusCode.Accepted, upgrade.StatusCode);
            Assert.Equal("AwaitingFinancialTerms", (await upgrade.Content.ReadFromJsonAsync<PlanChangeRequestResponse>())!.State);
            Assert.Equal("ESSENCIAL", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!.PlanCode);
            var cancelled = await tenant.Client.PostAsync(Billing + "/cancel", null);
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            var cancellation = (await cancelled.Content.ReadFromJsonAsync<LifecycleResponse>())!;
            Assert.Equal("CancellationScheduled", cancellation.State);
            Assert.Equal(renewed.NextChargeAt, cancellation.CancellationEffectiveAt);
            Assert.Equal("Active", cancellation.Status);
            Assert.Equal(1, gateway.CancellationPosts);
            gateway.Now = renewed.NextChargeAt!.Value.AddMilliseconds(-1);
            var create = new CreateServiceRequest("Ainda vigente", 5, 30);
            Assert.Equal(HttpStatusCode.Created, (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", create)).StatusCode);
            gateway.Now = renewed.NextChargeAt;
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetServices<IScheduledJob>().OfType<DelinquencyJob>().Single().ExecuteAsync();
            Assert.Equal("Cancelled", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>(Billing))!.Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await tenant.Client.PostAsJsonAsync("/api/v1/catalog/services", create)).StatusCode);
        }
        finally { gateway.Now = null; }
    }

    private Task SetProfessionalAsync(Guid tenantId, Guid professionalId, bool active) =>
        WithTenantAsync(tenantId, async db =>
        {
            await db.Professionals.Where(person => person.Id == professionalId)
                .ExecuteUpdateAsync(update => update.SetProperty(person => person.IsActive, active));
        });

    private async Task<HttpStatusCode> AttemptPublicBookingAsync(Guid tenantId)
    {
        var host = "e5-public-" + Guid.NewGuid().ToString("N")[..10];
        Guid professionalId = default, serviceId = default;
        await WithTenantAsync(tenantId, async db =>
        {
            var service = Service.Create(tenantId, "Corte público", Money.From(30m), 30);
            var professional = Professional.Create(tenantId, "Pessoa E5",
                WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(17, 0)));
            var site = Site.Create(tenantId, host);
            site.Pages.First().AddSection(SiteComponentType.Hero).SetProperty("title", "Reserve");
            site.Publish(Guid.NewGuid(), factory.Stub.Instant);
            db.AddRange(service, professional, ProfessionalService.Create(tenantId, professional.Id, service.Id), site);
            await db.SaveChangesAsync();
            professionalId = professional.Id;
            serviceId = service.Id;
        });
        using var anonymous = factory.CreateClient();
        using var response = await anonymous.PostAsJsonAsync($"/api/v1/public/site/appointments?host={host}",
            new PublicBookingRequest("Cliente E5", "21999114444", null, serviceId, professionalId,
                new DateOnly(2027, 1, 4), new TimeOnly(9, 0)));
        return response.StatusCode;
    }

    private static async Task<LifecycleResponse> VerifyCycleAsync(HttpClient client)
    {
        using var response = await client.PostAsync(Billing + "/verify", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LifecycleResponse>())!;
    }

    [Fact]
    public async Task Trial_Continua_Vigente_E_Retomada_Apos_Expiracao_Usa_A_Mesma_Tentativa()
    {
        using var tenant = await SignUpAsync();
        var gateway = factory.Stub;
        gateway.Reset();
        var request = new StartCheckoutRequest(Guid.NewGuid(), "PRO");
        var initial = await StartAsync(tenant.Client, request, "WaitingForTrialEnd");
        Assert.NotNull(initial.ResumeOn);
        Assert.Null(initial.CheckoutUrl);
        Assert.Equal(0, gateway.Posts);
        var subscription = await tenant.Client.GetFromJsonAsync<SubscriptionResponse>("/api/v1/billing/subscription");
        Assert.Equal("Trialing", subscription!.Status);
        Assert.NotEqual(initial.AttemptId, subscription.Id);
        Assert.Equal(initial, await StartAsync(tenant.Client, request, "WaitingForTrialEnd"));

        await AgeTrialAsync(tenant.Id);
        var resumed = await StartAsync(tenant.Client, request, "AwaitingAuthorization");
        Assert.Equal(initial.AttemptId, resumed.AttemptId);
        Assert.NotNull(resumed.CheckoutUrl);
        Assert.Equal(1, gateway.Posts);
    }

    [Fact]
    public async Task Concorrencia_Tenant_E_Pagamento_Provado_Protegem_Uma_Unica_Ativacao()
    {
        using var tenant = await SignUpAsync();
        using var other = await SignUpAsync();
        await AgeTrialAsync(tenant.Id);
        using var receptionist = await SignInReceptionistAsync(tenant.Id);
        var gateway = factory.Stub;
        gateway.Reset();
        gateway.ReleasePost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new StartCheckoutRequest(Guid.NewGuid(), "PRO");
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionist.PostAsJsonAsync(Checkout, request)).StatusCode);

        var start = StartAsync(tenant.Client, request, "AwaitingAuthorization");
        try
        {
            await gateway.PostEntered.Task.WaitAsync(TimeSpan.FromSeconds(12));
            var replay = await StartAsync(tenant.Client, request, "RequiresReconciliation");
            Assert.Equal(HttpStatusCode.Conflict,
                (await tenant.Client.PostAsJsonAsync(Checkout, request with { CheckoutKey = Guid.NewGuid() })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await tenant.Client.PostAsJsonAsync(Checkout, request with { PlanCode = "ESSENCIAL" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync($"{Checkout}/{request.CheckoutKey}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.Client.PostAsync($"{Checkout}/{request.CheckoutKey}/verify", null)).StatusCode);
            Assert.Equal(1, gateway.Posts);
            await WithTenantAsync(tenant.Id, async db =>
            {
                var saved = await db.Subscriptions.SingleAsync(item => item.CheckoutKey == request.CheckoutKey);
                Assert.Equal(replay.AttemptId, saved.Id);
                Assert.NotNull(saved.CheckoutDispatchedAt);
                Assert.Null(saved.GatewaySubscriptionId);
            });
        }
        finally
        {
            gateway.ReleasePost.TrySetResult();
        }
        var created = await start;
        Assert.Equal(created, await StartAsync(tenant.Client, request, "AwaitingAuthorization"));
        var forged = await tenant.Client.PostAsJsonAsync($"{Checkout}/{request.CheckoutKey}/verify",
            new { preapproval_id = "outra-assinatura", payment_id = 1234567, status = "approved" });
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        Assert.Equal("AwaitingAuthorization", (await forged.Content.ReadFromJsonAsync<CheckoutResponse>())!.State);
        gateway.InvalidReference = true;
        Assert.Equal("RequiresReconciliation", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
        gateway.InvalidReference = false;
        gateway.Authorized = true;
        Assert.Equal("AwaitingFinancialConfirmation", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
        Assert.Equal("Trialing", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>("/api/v1/billing/subscription"))!.Status);
        gateway.Approved = true;
        gateway.EarlierUnpaid = true;
        Assert.Equal("AwaitingFinancialConfirmation", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
        gateway.EarlierUnpaid = false;
        gateway.Refunded = true;
        Assert.Equal("AwaitingFinancialConfirmation", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
        gateway.Refunded = false;

        var confirmations = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            tenant.Client.PostAsync($"{Checkout}/{request.CheckoutKey}/verify", null)));
        Assert.Contains(confirmations, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(confirmations, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        Assert.Equal("Active", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
        var current = await tenant.Client.GetFromJsonAsync<SubscriptionResponse>("/api/v1/billing/subscription");
        Assert.Equal(created.AttemptId, current!.Id);
        Assert.Equal("PRO", current.PlanCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await tenant.Client.PostAsJsonAsync(Checkout, request with { CheckoutKey = Guid.NewGuid() })).StatusCode);
        await WithTenantAsync(tenant.Id, async db =>
        {
            Assert.Single(await db.PlanChanges.Where(change => change.SubscriptionId == created.AttemptId).ToListAsync());
            Assert.Single(await db.Subscriptions.Where(item => item.Status == SubscriptionStatus.Expired).ToListAsync());
        });
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("ambiguous")]
    [InlineData("refused")]
    public async Task Falhas_Do_Post_Nao_Repetem_Envio_E_Somente_Recusa_Libera_Outra_Chave(string outcome)
    {
        using var tenant = await SignUpAsync();
        await AgeTrialAsync(tenant.Id);
        factory.Stub.Reset(outcome);
        var request = new StartCheckoutRequest(Guid.NewGuid(), "PRO");
        var state = outcome == "refused" ? "Failed" : "RequiresReconciliation";
        var first = await StartAsync(tenant.Client, request, state);
        Assert.Equal(first, await StartAsync(tenant.Client, request, state));
        Assert.Equal(first, await VerifyAsync(tenant.Client, request.CheckoutKey));
        Assert.Equal(1, factory.Stub.Posts);
        var newKey = request with { CheckoutKey = Guid.NewGuid() };
        if (outcome == "refused")
        {
            await StartAsync(tenant.Client, newKey, "Failed");
            Assert.Equal(2, factory.Stub.Posts);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, (await tenant.Client.PostAsJsonAsync(Checkout, newKey)).StatusCode);
            Assert.Equal(1, factory.Stub.Posts);
        }
        Assert.Equal("Trialing", (await tenant.Client.GetFromJsonAsync<SubscriptionResponse>("/api/v1/billing/subscription"))!.Status);
    }

    [Fact]
    public async Task Capacidade_Revalidada_E_Pagamento_Duplicado_Revertem_Ativacao()
    {
        using var tenant = await SignUpAsync();
        using var other = await SignUpAsync();
        await AgeTrialAsync(tenant.Id);
        await AgeTrialAsync(other.Id);
        var gateway = factory.Stub;
        gateway.Reset();
        var code = "E4-" + Guid.NewGuid().ToString("N")[..8];
        await WithTenantAsync(tenant.Id, async db =>
        {
            db.Plans.Add(Plan.Create(code, "Teste E4", Money.From(99m), 1, SiteCustomizationLevel.Basic, 0));
            await db.SaveChangesAsync();
        });
        var request = new StartCheckoutRequest(Guid.NewGuid(), code);
        await StartAsync(tenant.Client, request, "AwaitingAuthorization");
        var otherRequest = new StartCheckoutRequest(Guid.NewGuid(), code);
        await StartAsync(other.Client, otherRequest, "AwaitingAuthorization");
        Guid professionalId = default;
        await WithTenantAsync(tenant.Id, async db =>
        {
            var service = Service.Create(tenant.Id, "Serviço", Money.From(20m), 30);
            db.Services.Add(service);
            for (var i = 0; i < 2; i++)
            {
                var person = Professional.Create(tenant.Id, $"Profissional {i}",
                    WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(17, 0)));
                db.Professionals.Add(person);
                db.ProfessionalServices.Add(ProfessionalService.Create(tenant.Id, person.Id, service.Id));
                professionalId = person.Id;
            }
            await db.SaveChangesAsync();
        });
        gateway.Authorized = gateway.Approved = true;
        Assert.Equal("CapacityExceeded", (await VerifyAsync(tenant.Client, request.CheckoutKey)).State);
        Assert.Equal("Active", (await VerifyAsync(other.Client, otherRequest.CheckoutKey)).State);
        await WithTenantAsync(tenant.Id, async db =>
        {
            await db.ProfessionalServices.Where(item => item.ProfessionalId == professionalId).ExecuteDeleteAsync();
        });
        // O dublê devolve o mesmo ID de pagamento para as duas autorizações.
        Assert.Equal(HttpStatusCode.Conflict,
            (await tenant.Client.PostAsync($"{Checkout}/{request.CheckoutKey}/verify", null)).StatusCode);
        await WithTenantAsync(tenant.Id, async db =>
        {
            Assert.Single(await db.Subscriptions.Where(item => item.Status == SubscriptionStatus.Trialing).ToListAsync());
            Assert.False(await db.PlanChanges.AnyAsync());
            Assert.Null((await db.Subscriptions.SingleAsync(item => item.CheckoutKey == request.CheckoutKey)).FirstPaymentId);
        });
    }

    private static async Task<CheckoutResponse> StartAsync(HttpClient client, StartCheckoutRequest request, string state)
    {
        using var response = await client.PostAsJsonAsync(Checkout, request);
        Assert.Equal(state == "Failed" ? HttpStatusCode.OK : HttpStatusCode.Accepted, response.StatusCode);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResponse>())!;
        Assert.Equal(state, checkout.State);
        return checkout;
    }

    private static async Task<CheckoutResponse> VerifyAsync(HttpClient client, Guid key)
    {
        using var response = await client.PostAsync($"{Checkout}/{key}/verify", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutResponse>())!;
    }

    private async Task<TestTenant> SignUpAsync(bool legacy = true)
    {
        var slug = "checkout-" + Guid.NewGuid().ToString("N")[..12];
        var email = slug + "@example.invalid";
        using var anonymous = factory.CreateClient();
        using var signUp = await anonymous.PostAsJsonAsync("/api/v1/public/signup", new
        {
            slug, name = "Estabelecimento E4", segment = 1, document = "12345678000190", ownerName = "Owner E4",
            ownerEmail = email, ownerPassword = Password
        });
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);
        var data = await signUp.Content.ReadFromJsonAsync<JsonElement>();
        var id = data.GetProperty("tenantId").GetGuid();
        if (legacy)
        {
            await WithTenantAsync(id, async db =>
            {
                var end = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
                await db.Subscriptions.ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, SubscriptionStatus.Trialing)
                    .SetProperty(item => item.TrialEndsAt, end)
                    .SetProperty(item => item.CurrentPeriodEnd, end));
            });
        }
        return new TestTenant(id, await SignInAsync(id, email));
    }

    private async Task<HttpClient> SignInReceptionistAsync(Guid tenantId)
    {
        const string email = "reception@example.invalid";
        await WithTenantAsync(tenantId, async db =>
        {
            using var scope = factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Users.Add(User.CreateForTenant(tenantId, "Recepção", email, hasher.Hash(Password),
                Role.Receptionist, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        return await SignInAsync(tenantId, email);
    }

    private async Task<HttpClient> SignInAsync(Guid tenantId, string email)
    {
        var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(tenantId, email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private Task AgeTrialAsync(Guid tenant) => WithTenantAsync(tenant, async db =>
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await db.Subscriptions.Where(item => item.Status == SubscriptionStatus.Trialing).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.CurrentPeriodStart, today.AddDays(-14))
            .SetProperty(item => item.CurrentPeriodEnd, today)
            .SetProperty(item => item.TrialEndsAt, today));
    });

    private async Task WithTenantAsync(Guid tenant, Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant);
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private sealed record TestTenant(Guid Id, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}

public sealed class CheckoutApiFactory : IsolatedApiFactory
{
    public CheckoutProviderStub Stub { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRecurringCheckoutGateway>();
            services.RemoveAll<ISignupThrottle>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new CheckoutClock(Stub));
            services.AddSingleton<ISignupThrottle, UnlimitedSignupThrottle>();
            services.AddScoped<IRecurringCheckoutGateway>(_ =>
                new MercadoPagoGateway(new HttpClient(Stub, disposeHandler: false), new MercadoPagoOptions
                {
                    AccessToken = "fake-token",
                    ReturnUrl = "https://servia.example/checkout-return",
                    TrialPlanIds = new() { ["PRO"] = "trial_pro" }
                }));
        });
    }

    private sealed class CheckoutClock(CheckoutProviderStub stub) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => stub.Instant;
    }
}

public sealed class CheckoutProviderStub : HttpMessageHandler
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,
        (string Reference, decimal Amount, string? TrialPlan, DateTimeOffset Created)> _attempts = new();
    private static long _nextPaymentId = 1234567;
    private int _posts;
    private long _paymentId;

    public int Posts => Volatile.Read(ref _posts);
    public bool Authorized { get; set; }
    public bool Approved { get; set; }
    public bool Refunded { get; set; }
    public bool InvalidReference { get; set; }
    public bool EarlierUnpaid { get; set; }
    public bool Rejected { get; set; }
    public bool Cancelled { get; set; }
    public bool CancellationTimeout { get; set; }
    public int CancellationPosts { get; private set; }
    public int TrialDays { get; set; } = 14;
    public bool CreditOnly { get; set; } = true;
    public DateTimeOffset? CycleDue { get; set; }
    public DateTimeOffset? Now { get; set; }
    public DateTimeOffset Instant => Now ?? DateTimeOffset.UtcNow;
    public decimal? NewRecurringAmount { get; set; }
    public string Outcome { get; private set; } = "";
    public TaskCompletionSource? ReleasePost { get; set; }
    public TaskCompletionSource PostEntered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset(string outcome = "")
    {
        Authorized = Approved = Refunded = InvalidReference = EarlierUnpaid = Rejected = Cancelled = CancellationTimeout = false;
        CancellationPosts = 0;
        NewPayment();
        TrialDays = 14;
        CreditOnly = true;
        CycleDue = null;
        Now = null;
        NewRecurringAmount = null;
        Outcome = outcome;
        ReleasePost = null;
        PostEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _attempts.Clear();
        _posts = 0;
    }

    public void NewPayment() => _paymentId = Interlocked.Increment(ref _nextPaymentId);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Assert.Equal("api.mercadopago.com", uri.Host);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        if (uri.AbsolutePath == "/preapproval_plan/trial_pro")
        {
            return Reply(HttpStatusCode.OK, new
            {
                id = "trial_pro", status = "active",
                payment_methods_allowed = new { payment_types = new[] { new { id = CreditOnly ? "credit_card" : "debit_card" } } },
                auto_recurring = new { frequency = 1, frequency_type = "months", currency_id = "BRL",
                    transaction_amount = 199m, free_trial = new { frequency = TrialDays, frequency_type = "days" } }
            });
        }
        if (request.Method == HttpMethod.Put)
        {
            CancellationPosts++;
            if (CancellationTimeout) throw new TaskCanceledException();
            Cancelled = true;
            return Authorization(uri.Segments[^1], "cancelled");
        }
        if (request.Method == HttpMethod.Post)
        {
            Assert.Equal("/preapproval", uri.AbsolutePath);
            Assert.False(request.Headers.Contains("X-Idempotency-Key"));
            Interlocked.Increment(ref _posts);
            var create = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var trialPlan = create.TryGetProperty("preapproval_plan_id", out var plan) ? plan.GetString() : null;
            Assert.Equal(trialPlan is null ? "pending" : "authorized", create.GetProperty("status").GetString());
            Assert.EndsWith("@example.invalid", create.GetProperty("payer_email").GetString());
            var amount = 199m;
            if (trialPlan is null)
            {
                var recurrence = create.GetProperty("auto_recurring");
                Assert.False(recurrence.TryGetProperty("start_date", out _));
                Assert.False(create.TryGetProperty("card_token_id", out _));
                amount = recurrence.GetProperty("transaction_amount").GetDecimal();
            }
            else
            {
                Assert.Equal("trial_pro", trialPlan);
                Assert.Equal("opaque-test-token", create.GetProperty("card_token_id").GetString());
                Assert.False(create.TryGetProperty("auto_recurring", out _));
            }
            Assert.Contains("checkoutKey=", create.GetProperty("back_url").GetString());
            var reference = create.GetProperty("external_reference").GetString()!;
            var id = $"sub_{reference}";
            _attempts[id] = (reference, amount, trialPlan, Instant);
            PostEntered.TrySetResult();
            if (ReleasePost is { } release)
            {
                await release.Task.WaitAsync(cancellationToken);
            }
            if (Outcome == "timeout") throw new TaskCanceledException();
            if (Outcome == "refused") return Reply(HttpStatusCode.BadRequest, new { message = "refused" });
            if (Outcome == "ambiguous") return Reply(HttpStatusCode.Created, new { id });
            return Authorization(id, trialPlan is null ? "pending" : "authorized", HttpStatusCode.Created);
        }
        if (uri.AbsolutePath.StartsWith("/preapproval/", StringComparison.Ordinal))
        {
            var id = uri.Segments[^1];
            return Authorization(id, Cancelled ? "canceled"
                : Authorized || _attempts[id].TrialPlan is not null ? "authorized" : "pending");
        }
        if (uri.AbsolutePath == "/authorized_payments/search")
        {
            var id = uri.Query.Split('&')[0].Split('=')[1];
            var attempt = _attempts[id];
            var now = CycleDue ?? Instant;
            object Invoice(DateTimeOffset dueAt, string status) => new
            {
                type = "scheduled", preapproval_id = id, debit_date = dueAt,
                currency_id = "BRL", transaction_amount = NewRecurringAmount ?? attempt.Amount,
                payment = new { id = _paymentId, status }
            };
            var latest = Invoice(now, Approved ? "approved" : Rejected ? "rejected" : "pending");
            var results = EarlierUnpaid ? new[] { latest, Invoice(now.AddDays(-1), "pending") } : new[] { latest };
            return Reply(HttpStatusCode.OK, new
            {
                paging = new { offset = 0, total = results.Length },
                results
            });
        }
        Assert.Equal($"/v1/payments/{_paymentId}", uri.AbsolutePath);
        return Reply(HttpStatusCode.OK, new
        {
            id = _paymentId, status = Rejected ? "rejected" : Approved ? "approved" : "pending",
            currency_id = "BRL", date_approved = Instant,
            transaction_amount = NewRecurringAmount ?? _attempts.Values.First().Amount, transaction_amount_refunded = Refunded ? 1 : 0
        });
    }

    private HttpResponseMessage Authorization(string id, string status, HttpStatusCode code = HttpStatusCode.OK)
    {
        var attempt = _attempts[id];
        return Reply(code, new
        {
            id, status, external_reference = InvalidReference ? "foreign" : attempt.Reference,
            init_point = attempt.TrialPlan is null ? $"https://www.mercadopago.com.br/subscriptions/checkout?preapproval_id={id}" : null,
            preapproval_plan_id = attempt.TrialPlan, date_created = attempt.Created,
            next_payment_date = attempt.TrialPlan is null ? (DateTimeOffset?)null : attempt.Created.AddDays(14),
            auto_recurring = new { currency_id = "BRL", frequency = 1, frequency_type = "months",
                transaction_amount = NewRecurringAmount ?? attempt.Amount }
        });
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };
}
