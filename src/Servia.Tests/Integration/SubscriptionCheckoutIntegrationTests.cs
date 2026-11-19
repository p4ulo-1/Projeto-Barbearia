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
using Servia.Domain.Appointments;
using Servia.Domain.Billing;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Identity;
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

    private async Task<TestTenant> SignUpAsync()
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
            services.AddSingleton<ISignupThrottle, UnlimitedSignupThrottle>();
            services.AddScoped<IRecurringCheckoutGateway>(_ =>
                new MercadoPagoGateway(new HttpClient(Stub, disposeHandler: false), new MercadoPagoOptions
                {
                    AccessToken = "fake-token",
                    ReturnUrl = "https://servia.example/checkout-return"
                }));
        });
    }
}

public sealed class CheckoutProviderStub : HttpMessageHandler
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Reference, decimal Amount)> _attempts = new();
    private static long _nextPaymentId = 1234567;
    private int _posts;
    private long _paymentId;

    public int Posts => Volatile.Read(ref _posts);
    public bool Authorized { get; set; }
    public bool Approved { get; set; }
    public bool Refunded { get; set; }
    public bool InvalidReference { get; set; }
    public bool EarlierUnpaid { get; set; }
    public string Outcome { get; private set; } = "";
    public TaskCompletionSource? ReleasePost { get; set; }
    public TaskCompletionSource PostEntered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset(string outcome = "")
    {
        Authorized = Approved = Refunded = InvalidReference = EarlierUnpaid = false;
        _paymentId = Interlocked.Increment(ref _nextPaymentId);
        Outcome = outcome;
        ReleasePost = null;
        PostEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _attempts.Clear();
        _posts = 0;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Assert.Equal("api.mercadopago.com", uri.Host);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        if (request.Method == HttpMethod.Post)
        {
            Assert.Equal("/preapproval", uri.AbsolutePath);
            Assert.False(request.Headers.Contains("X-Idempotency-Key"));
            Interlocked.Increment(ref _posts);
            var create = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var recurrence = create.GetProperty("auto_recurring");
            Assert.Equal("pending", create.GetProperty("status").GetString());
            Assert.EndsWith("@example.invalid", create.GetProperty("payer_email").GetString());
            Assert.False(create.TryGetProperty("card_token_id", out _));
            Assert.False(recurrence.TryGetProperty("start_date", out _));
            Assert.Contains("checkoutKey=", create.GetProperty("back_url").GetString());
            var reference = create.GetProperty("external_reference").GetString()!;
            var id = $"sub_{reference}";
            _attempts[id] = (reference, recurrence.GetProperty("transaction_amount").GetDecimal());
            PostEntered.TrySetResult();
            if (ReleasePost is { } release)
            {
                await release.Task.WaitAsync(cancellationToken);
            }
            if (Outcome == "timeout") throw new TaskCanceledException();
            if (Outcome == "refused") return Reply(HttpStatusCode.BadRequest, new { message = "refused" });
            if (Outcome == "ambiguous") return Reply(HttpStatusCode.Created, new { id });
            return Authorization(id, "pending", HttpStatusCode.Created);
        }
        if (uri.AbsolutePath.StartsWith("/preapproval/", StringComparison.Ordinal))
        {
            return Authorization(uri.Segments[^1], Authorized ? "authorized" : "pending");
        }
        if (uri.AbsolutePath == "/authorized_payments/search")
        {
            var id = uri.Query.Split('&')[0].Split('=')[1];
            var attempt = _attempts[id];
            var now = DateTimeOffset.UtcNow;
            object Invoice(DateTimeOffset dueAt, string status) => new
            {
                type = "scheduled", preapproval_id = id, debit_date = dueAt,
                currency_id = "BRL", transaction_amount = attempt.Amount,
                payment = new { id = _paymentId, status }
            };
            var latest = Invoice(now, Approved ? "approved" : "pending");
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
            id = _paymentId, status = "approved", currency_id = "BRL", date_approved = DateTimeOffset.UtcNow,
            transaction_amount = _attempts.Values.First().Amount, transaction_amount_refunded = Refunded ? 1 : 0
        });
    }

    private HttpResponseMessage Authorization(string id, string status, HttpStatusCode code = HttpStatusCode.OK)
    {
        var attempt = _attempts[id];
        return Reply(code, new
        {
            id, status, external_reference = InvalidReference ? "foreign" : attempt.Reference,
            init_point = $"https://www.mercadopago.com.br/subscriptions/checkout?preapproval_id={id}",
            auto_recurring = new { currency_id = "BRL", frequency = 1, frequency_type = "months", transaction_amount = attempt.Amount }
        });
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };
}
