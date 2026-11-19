using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Integration;

public sealed class ProfessionalAvailabilityIntegrationTests(PublicSiteApiFactory factory)
    : IClassFixture<PublicSiteApiFactory>
{
    [Fact]
    public async Task Jornada_E_Bloqueio_Valem_Nos_Dois_Canais_E_Preservam_Compromissos()
    {
        var booking = await factory.RegisterPublicBookingDataAsync();
        using var visitor = factory.CreateClient();
        using var owner = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Where(tenant => tenant.Slug == PublicSiteApiFactory.PublishedHost)
            .Select(tenant => tenant.Id).SingleAsync();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
        var counts = scope.ServiceProvider.GetRequiredService<IProfessionalCountReader>();
        const string email = "owner-jornada@teste.local";
        const string password = "SenhaSegura123";
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(User.CreateForTenant(tenantId, "Owner", email, hasher.Hash(password), Role.Owner, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var login = await owner.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(tenantId, email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var date = booking.Date.AddDays(14);
        var originalTime = new TimeOnly(10, 0);
        var startTime = new TimeOnly(11, 0);
        var nextTime = new TimeOnly(11, 30);
        const string host = PublicSiteApiFactory.PublishedHost;
        const string agenda = "/api/v1/agenda/appointments";
        var professionalRoute = $"/api/v1/team/professionals/{booking.ProfessionalId}";
        var bookingRoute = $"/api/v1/public/site/appointments?host={host}";
        var query = $"professionalId={booking.ProfessionalId}&serviceId={booking.ServiceId}&date={date:yyyy-MM-dd}";

        object PublicRequest(string phone, TimeOnly time) => new
        {
            name = "Cliente da agenda",
            phone,
            booking.ProfessionalId,
            booking.ServiceId,
            date,
            time
        };

        async Task AssertSlotsAsync(params TimeOnly[] expected)
        {
            var privateSlots = (await owner.GetFromJsonAsync<AvailabilityResponse>(
                $"/api/v1/agenda/availability?{query}"))!;
            var publicSlots = (await visitor.GetFromJsonAsync<PublicAvailabilityResponse>(
                $"/api/v1/public/site/availability?host={host}&{query}"))!;
            Assert.Equal(expected, privateSlots.AvailableSlots.ToArray());
            Assert.Equal(expected, publicSlots.Slots.ToArray());
        }

        var created = await visitor.PostAsJsonAsync(bookingRoute, PublicRequest("21999110001", originalTime));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appointment = (await created.Content.ReadFromJsonAsync<PublicAppointmentResponse>())!;
        var customerId = await db.Appointments.Where(item => item.Id == appointment.Id)
            .Select(item => item.CustomerId).SingleAsync();
        var rescheduleRoute = $"{agenda}/{appointment.Id}/reschedule";

        var closedBusiness = new UpdateScheduleRequest(Enum.GetValues<DayOfWeek>()
            .Select(day => new ScheduleDayRequest(day, false, null, null)).ToArray());
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/v1/tenant/settings/schedule", closedBusiness)).StatusCode);
        var schedule = new UpdateProfessionalScheduleRequest([date.DayOfWeek], startTime, new TimeOnly(13, 0));
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(professionalRoute + "/schedule", schedule)).StatusCode);
        Assert.Equal(1, await counts.CountEligibleAsync(tenantId));
        await AssertSlotsAsync(startTime, nextTime, new TimeOnly(12, 0), new TimeOnly(12, 30));

        var start = new DateTimeOffset(date.ToDateTime(startTime), TimeSpan.Zero);
        var block = await owner.PostAsJsonAsync(professionalRoute + "/blocks",
            new CreateBlockRequest(start, start.AddMinutes(30), "Compromisso"));
        Assert.Equal(HttpStatusCode.Created, block.StatusCode);
        await AssertSlotsAsync(nextTime, new TimeOnly(12, 0), new TimeOnly(12, 30));
        Assert.Equal(1, await counts.CountEligibleAsync(tenantId));

        const string rejectedPhone = "21999110002";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await visitor.PostAsJsonAsync(bookingRoute, PublicRequest(rejectedPhone, startTime))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(agenda,
            new CreateAppointmentRequest(customerId, booking.ServiceId, booking.ProfessionalId, date, originalTime))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PatchAsJsonAsync(rescheduleRoute, new RescheduleRequest(date, startTime))).StatusCode);
        Assert.False(await db.Customers.AnyAsync(item => item.Phone == rejectedPhone));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(block.Headers.Location)).StatusCode);
        await AssertSlotsAsync(startTime, nextTime, new TimeOnly(12, 0), new TimeOnly(12, 30));
        Assert.Equal(1, await counts.CountEligibleAsync(tenantId));
        Assert.Equal(HttpStatusCode.Created,
            (await visitor.PostAsJsonAsync(bookingRoute, PublicRequest("21999110003", startTime))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PatchAsJsonAsync(rescheduleRoute, new RescheduleRequest(date, startTime))).StatusCode);

        var records = await owner.GetFromJsonAsync<AppointmentResponse[]>($"{agenda}?date={date:yyyy-MM-dd}");
        var original = Assert.Single(records!, item => item.Id == appointment.Id);
        Assert.Equal(originalTime, original.Time);
        Assert.Equal(30, original.DurationMinutes);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PatchAsJsonAsync(rescheduleRoute, new RescheduleRequest(date, nextTime))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(professionalRoute + "/schedule",
            schedule with { WorkDays = [] })).StatusCode);
        await AssertSlotsAsync();
        Assert.Equal(0, await counts.CountEligibleAsync(tenantId));
    }
}
