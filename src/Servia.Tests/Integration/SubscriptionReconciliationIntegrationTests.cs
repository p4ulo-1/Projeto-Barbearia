using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Api.Jobs;
using Servia.Application.Jobs;
using Servia.Domain.Billing;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Integration;

public sealed class SubscriptionReconciliationIntegrationTests(CheckoutApiFactory factory)
    : IClassFixture<CheckoutApiFactory>
{
    private const string Billing = "/api/v1/billing/subscription";

    [Fact]
    public async Task Reconcilia_Checkout_Ambiguo_E_Ciclos_Sem_Webhook_Com_Concorrencia_E_Sem_Operacoes_Duplicadas()
    {
        var gateway = factory.Stub;
        gateway.Reset("timeout");
        var dispatch = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        gateway.Now = dispatch;
        using var tenant = await SignUpAsync();
        using var other = await SignUpAsync();
        using var anonymous = factory.CreateClient();
        var request = new StartCheckoutRequest(Guid.NewGuid(), "PRO", "opaque-test-token");
        var checkout = await StartAsync(tenant, request);
        var id = checkout.AttemptId;
        var externalId = $"sub_{id:N}";
        gateway.NotifiedSubscription = externalId;
        Assert.Equal("RequiresReconciliation", checkout.State);
        Assert.Equal(1, gateway.Posts);
        Assert.False(await ProcessNextAsync());

        gateway.Now = dispatch.AddMinutes(1);
        gateway.SearchBehavior = "missing";
        var unknown = await VerifyAsync(id, "NoRemoteMatch");
        Assert.False(await ProcessNextAsync());
        Assert.Equal("RequiresReconciliation", (await StartAsync(tenant, request)).State);
        Assert.Equal(1, gateway.Posts);

        gateway.Now = unknown.NextCheckAt;
        gateway.SearchBehavior = "throttle";
        var throttled = await VerifyAsync(id, "RateLimited");
        Assert.Equal(gateway.Instant.AddMinutes(20), throttled.RateLimitedUntil);
        Assert.Equal(throttled.RateLimitedUntil, throttled.NextCheckAt);
        Assert.False(await ProcessNextAsync());
        Assert.Equal(2, gateway.Searches);

        gateway.Now = throttled.NextCheckAt;
        gateway.SearchBehavior = "duplicate";
        var duplicate = await VerifyAsync(id, "AmbiguousMatch");
        Assert.True(duplicate.NeedsIntervention);
        var ambiguousDelivery = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, await SubscriptionCheckoutIntegrationTests.NotifyAsync(anonymous,
            MercadoPagoNotification.Subscription, externalId, ambiguousDelivery));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetServices<IScheduledJob>().OfType<MercadoPagoInboxJob>().Single();
            Assert.True(await inbox.ProcessNextAsync(default));
        }
        Assert.Equal("UnlinkedResource", await WithDbAsync(db => db.Set<MercadoPagoNotification>()
            .Where(row => row.ExternalId == ambiguousDelivery).Select(row => row.Result).SingleAsync()));
        Assert.Equal("AwaitingTrialAuthorization", (await ReadSubscriptionAsync(tenant)).Status);
        Assert.Equal(1, gateway.Posts);

        gateway.Now = duplicate.NextCheckAt;
        gateway.SearchBehavior = null;
        gateway.NotificationUnavailable = true;
        var failure = await VerifyAsync(id, "VerificationFailed");
        gateway.Now = failure.NextCheckAt;
        gateway.NotificationUnavailable = false;
        gateway.AuthorizationPending = true;
        var pendingAuthorization = await VerifyAsync(id, "AwaitingExternalConfirmation");
        Assert.Equal("AwaitingTrialAuthorization", (await ReadSubscriptionAsync(tenant)).Status);
        gateway.Now = pendingAuthorization.NextCheckAt;
        gateway.AuthorizationPending = false;
        var recovered = await VerifyAsync(id, "Trialing");
        Assert.False(recovered.NeedsIntervention);
        var trial = await ReadSubscriptionAsync(tenant);
        Assert.Equal(dispatch, trial.TrialStartedAtUtc);
        var firstDue = dispatch.AddDays(14);
        Assert.Equal(firstDue, trial.TrialEndsAtUtc);
        Assert.Equal("ESSENCIAL", trial.PlanCode);
        Assert.Equal("AwaitingTrialAuthorization", (await ReadSubscriptionAsync(other)).Status);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetServices<IScheduledJob>().OfType<MercadoPagoInboxJob>().Single();
            Assert.True(await inbox.ProcessNextAsync(default));
        }
        Assert.NotNull(await WithDbAsync(db => db.Set<MercadoPagoNotification>()
            .Where(row => row.ExternalId == ambiguousDelivery).Select(row => row.CompletedAt).SingleAsync()));
        Assert.Null(await WithDbAsync(db => db.Subscriptions.IgnoreQueryFilters().Where(row => row.Id == id)
            .Select(row => row.CheckoutPayerEmail).SingleAsync()));

        gateway.Now = gateway.CycleDue = firstDue;
        gateway.Rejected = true;
        var rejected = await VerifyAsync(id, "FirstPaymentRejected");
        gateway.Now = rejected.NextCheckAt;
        gateway.Rejected = false;
        gateway.Approved = true;
        await VerifyAsync(id, "Active");
        var paid = await ReadSubscriptionAsync(tenant);
        Assert.Equal("PRO", paid.PlanCode);
        var renewalDue = paid.NextChargeAtUtc!.Value;

        gateway.Now = gateway.CycleDue = renewalDue;
        gateway.NewPayment();
        gateway.Approved = false;
        await VerifyAsync(id, "PaymentPending");
        Assert.Equal("PastDue", (await ReadSubscriptionAsync(tenant)).Status);
        gateway.Now = renewalDue.AddHours(48);
        gateway.Approved = true;
        await VerifyAsync(id, "Renewed");
        Assert.Equal("Active", (await ReadSubscriptionAsync(tenant)).Status);
        Assert.False(await ProcessNextAsync());

        gateway.Now = gateway.CycleDue = renewalDue.AddMonths(1);
        gateway.NewPayment();
        var delivery = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK, await SubscriptionCheckoutIntegrationTests.NotifyAsync(anonymous,
            MercadoPagoNotification.AuthorizedPayment, gateway.InvoiceId, delivery));
        var cycleRequests = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.BeforeCycleResponse = async token =>
        {
            if (Interlocked.Increment(ref cycleRequests) == 2) both.SetResult();
            await both.Task.WaitAsync(token);
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var reconciliation = scope.ServiceProvider.GetServices<IScheduledJob>().OfType<SubscriptionReconciliationJob>().Single();
            var inbox = scope.ServiceProvider.GetServices<IScheduledJob>().OfType<MercadoPagoInboxJob>().Single();
            var concurrent = await Task.WhenAll(reconciliation.ProcessNextAsync(default), inbox.ProcessNextAsync(default));
            Assert.All(concurrent, Assert.True);
        }
        gateway.BeforeCycleResponse = null;
        Assert.Equal(2, cycleRequests);
        Assert.Equal(renewalDue.AddMonths(2), (await ReadSubscriptionAsync(tenant)).NextChargeAtUtc);

        gateway.CancellationTimeout = true;
        using var cancel = await tenant.PostAsync($"{Billing}/cancel", null);
        Assert.Equal(HttpStatusCode.Accepted, cancel.StatusCode);
        var state = await WithDbAsync(db => db.Set<SubscriptionReconciliation>().SingleAsync(item => item.SubscriptionId == id));
        gateway.Now = state.NextCheckAt;
        var unresolved = await VerifyAsync(id, "CancellationPending");
        var missedPaidCycle = renewalDue.AddMonths(2);
        Assert.True(unresolved.NextCheckAt < missedPaidCycle);
        gateway.Now = gateway.CycleDue = missedPaidCycle;
        gateway.NewPayment();
        gateway.Cancelled = true;
        await VerifyAsync(id, "CancellationScheduled");
        var scheduled = await ReadSubscriptionAsync(tenant);
        Assert.Equal("Active", scheduled.Status);
        Assert.Equal(missedPaidCycle.AddMonths(1), scheduled.CancellationEffectiveAtUtc);
        Assert.Equal(1, gateway.Posts);
        Assert.Equal(1, gateway.CancellationPosts);

        await WithDbAsync(async db =>
        {
            var history = await db.PlanChanges.IgnoreQueryFilters().Where(row => row.SubscriptionId == id).ToListAsync();
            Assert.Single(history);
            var invoices = await db.PlatformInvoices.IgnoreQueryFilters().Where(row => row.SubscriptionId == id).ToListAsync();
            Assert.Equal(4, invoices.Count);
            Assert.Equal(4, invoices.Select(row => row.GatewayPaymentId).Distinct().Count());
            Assert.All(invoices, invoice => Assert.True(invoice.IsPaid));
            return true;
        });
    }

    [Fact]
    public async Task Recupera_Primeira_Cobranca_E_Trial_Originais_Quando_Provedor_Ja_Avancou_Vencimento()
    {
        var gateway = factory.Stub;
        gateway.Reset("timeout");
        var dispatch = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        gateway.Now = dispatch;
        using var tenant = await SignUpAsync();
        var checkout = await StartAsync(tenant, new StartCheckoutRequest(Guid.NewGuid(), "PRO", "opaque-test-token"));
        var firstDue = dispatch.AddDays(14);
        gateway.Now = firstDue.AddHours(1);
        gateway.CycleDue = firstDue;
        gateway.ProviderNextCharge = firstDue.AddMonths(1);
        gateway.Approved = true;
        gateway.Cancelled = true;

        await VerifyAsync(checkout.AttemptId, "CancellationScheduled");
        var subscription = await ReadSubscriptionAsync(tenant);
        Assert.Equal("Active", subscription.Status);
        Assert.Equal(dispatch, subscription.TrialStartedAtUtc);
        Assert.Equal(firstDue, subscription.TrialEndsAtUtc);
        Assert.Equal(firstDue.AddMonths(1), subscription.CancellationEffectiveAtUtc);
        Assert.Equal(1, gateway.Posts);
        Assert.Equal(0, gateway.CancellationPosts);
        Assert.False(await ProcessNextAsync());
    }

    private async Task<SubscriptionReconciliation> VerifyAsync(Guid id, string expected)
    {
        Assert.True(await ProcessNextAsync());
        var state = await WithDbAsync(db => db.Set<SubscriptionReconciliation>().SingleAsync(item => item.SubscriptionId == id));
        Assert.Equal(expected, state.Result);
        Assert.Equal(factory.Stub.Instant, state.LastCheckedAt);
        Assert.Null(state.LeaseId);
        return state;
    }

    private async Task<bool> ProcessNextAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IScheduledJob>().OfType<SubscriptionReconciliationJob>().Single();
        return await job.ProcessNextAsync(default);
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<SubscriptionResponse> ReadSubscriptionAsync(HttpClient tenant) =>
        (await tenant.GetFromJsonAsync<SubscriptionResponse>(Billing))!;

    private static async Task<CheckoutResponse> StartAsync(HttpClient tenant, StartCheckoutRequest request)
    {
        using var response = await tenant.PostAsJsonAsync($"{Billing}/checkout", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutResponse>())!;
    }

    private async Task<HttpClient> SignUpAsync()
    {
        const string password = "SenhaCheckout123!";
        var slug = $"reconciliation-{Guid.NewGuid():N}"[..28];
        var email = $"{slug}@example.invalid";
        var client = factory.CreateClient();
        using var signup = await client.PostAsJsonAsync("/api/v1/public/signup", new
        {
            slug, name = "Estabelecimento E6-B", segment = 1, document = "12345678000190",
            ownerName = "Owner E6-B", ownerEmail = email, ownerPassword = password
        });
        Assert.Equal(HttpStatusCode.Created, signup.StatusCode);
        var tenantId = (await signup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid();
        using var login = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(tenantId, email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
