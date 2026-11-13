using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Servia.Domain.Sites;
using Servia.Infrastructure.Integrations;

namespace Servia.Tests.Integration;

/// <summary>
/// Rate limiting do agendamento público na API real (C3), com a implementação
/// persistente no PostgreSQL.
///
/// Em Development o limite é desligado (testes vêm do mesmo IP); aqui ele é
/// forçado ligado, com limite pequeno, para provar o 429 end-to-end.
/// </summary>
public sealed class PublicBookingThrottleIntegrationTests : IClassFixture<LimitedBookingApiFactory>
{
    private const int Limit = 3;

    private readonly LimitedBookingApiFactory _factory;
    private readonly HttpClient _client;

    public PublicBookingThrottleIntegrationTests(LimitedBookingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSeeded();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exceder_O_Limite_Retorna_429()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var payload = new
        {
            name = "Cliente Limite",
            phone = "11933334444",
            email = (string?)null,
            serviceId = register.ServiceId,
            professionalId = register.ProfessionalId,
            date = register.Date.ToString("yyyy-MM-dd"),
            time = "09:00:00"
        };

        // As primeiras tentativas passam pelo limite (podem dar 201 ou 409).
        for (var attempt = 0; attempt < Limit; attempt++)
        {
            var response = await _client.PostAsJsonAsync(
                $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
                payload);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // A tentativa seguinte excede o limite.
        var exceeded = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
            payload);

        Assert.Equal(HttpStatusCode.TooManyRequests, exceeded.StatusCode);
    }
}

/// <summary>Fábrica que liga o rate limiting persistente mesmo em Development.</summary>
public sealed class LimitedBookingApiFactory : PublicSiteApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var throttleDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(IPublicBookingThrottle))
                .ToList();

            foreach (var descriptor in throttleDescriptors)
            {
                services.Remove(descriptor);
            }

            var optionsDescriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(PublicBookingThrottleOptions))
                .ToList();

            foreach (var descriptor in optionsDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton(new PublicBookingThrottleOptions
            {
                MaxAttempts = 3,
                WindowMinutes = 10
            });

            services.AddScoped<IPublicBookingThrottle, PostgresPublicBookingThrottle>();
        });
    }
}
