using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Tests.Api;

namespace Servia.Tests.Integration;

/// <summary>
/// Expediente do estabelecimento pela API real (C9).
///
/// Prova que o administrador configura horários diferentes por dia e dias
/// fechados, e que alterações posteriores são persistidas — tudo
/// independente dos profissionais.
/// </summary>
public sealed class TenantScheduleIntegrationTests : IClassFixture<IsolatedApiFactory>
{
    private const string Password = "SenhaSegura123";
    private const string OwnerEmail = "dono-agenda@exemplo.com";

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;

    public TenantScheduleIntegrationTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Administrador_Configura_E_Altera_Expediente()
    {
        var tenantId = SeedTenant();

        var token = await SignInAsync(tenantId);
        var auth = _factory.CreateClient();
        auth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Configura: segunda aberta 08-12, terça fechada, demais fechadas.
        var update = await auth.PutAsJsonAsync(
            "/api/v1/tenant/settings/schedule",
            new
            {
                days = BuildDays(
                    open: DayOfWeek.Monday,
                    from: "08:00:00",
                    to: "12:00:00")
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // Consulta reflete a configuração.
        var get = await auth.GetAsync("/api/v1/tenant/settings/schedule");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var schedule = await get.Content.ReadFromJsonAsync<ScheduleDto>();
        Assert.NotNull(schedule);

        var monday = schedule!.Days.Single(day => day.DayOfWeek == DayOfWeek.Monday);
        var tuesday = schedule.Days.Single(day => day.DayOfWeek == DayOfWeek.Tuesday);

        Assert.True(monday.IsOpen);
        Assert.Equal("08:00:00", monday.OpenTime);
        Assert.False(tuesday.IsOpen);

        // Altera o expediente: segunda 14-18 (horário diferente do anterior).
        var second = await auth.PutAsJsonAsync(
            "/api/v1/tenant/settings/schedule",
            new
            {
                days = BuildDays(
                    open: DayOfWeek.Monday,
                    from: "14:00:00",
                    to: "18:00:00")
            });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var after = await auth.GetFromJsonAsync<ScheduleDto>("/api/v1/tenant/settings/schedule");
        var mondayAfter = after!.Days.Single(day => day.DayOfWeek == DayOfWeek.Monday);

        Assert.Equal("14:00:00", mondayAfter.OpenTime);
        Assert.Equal("18:00:00", mondayAfter.CloseTime);
    }

    [Fact]
    public async Task Expediente_Invalido_E_Rejeitado()
    {
        var tenantId = SeedTenant();
        var token = await SignInAsync(tenantId);
        var auth = _factory.CreateClient();
        auth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Abertura posterior ao fechamento.
        var update = await auth.PutAsJsonAsync(
            "/api/v1/tenant/settings/schedule",
            new
            {
                days = BuildDays(open: DayOfWeek.Monday, from: "18:00:00", to: "08:00:00")
            });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    private static object[] BuildDays(DayOfWeek open, string from, string to)
    {
        return Enum.GetValues<DayOfWeek>()
            .Select(day => (object)new
            {
                dayOfWeek = (int)day,
                isOpen = day == open,
                openTime = day == open ? from : null,
                closeTime = day == open ? to : null
            })
            .ToArray();
    }

    /// <summary>Cria um tenant real com proprietário e devolve o Id do tenant.</summary>
    private Guid SeedTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var now = DateTimeOffset.UtcNow;
        var slug = "agenda-" + Guid.NewGuid().ToString("N")[..8];

        var tenant = Tenant.Create(slug, "Estabelecimento Agenda", SegmentType.Barbershop, "98765432000199", now);
        context.Tenants.Add(tenant);

        context.Users.Add(User.CreateForTenant(
            tenant.Id, "Dono Agenda", OwnerEmail, hasher.Hash(Password), Role.Owner, now));

        context.SaveChanges();

        return tenant.Id;
    }

    private async Task<string> SignInAsync(Guid tenantId)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            tenantId, OwnerEmail, Password));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        return body!.AccessToken;
    }

    private sealed record ScheduleDto(List<ScheduleDayDto> Days);

    private sealed record ScheduleDayDto(DayOfWeek DayOfWeek, bool IsOpen, string? OpenTime, string? CloseTime);
}
