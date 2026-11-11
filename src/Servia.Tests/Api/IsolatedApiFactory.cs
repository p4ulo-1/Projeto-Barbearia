using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Servia.Tests.Api;

/// <summary>
/// Fábrica de API com PostgreSQL 17 isolado por classe de teste (Etapa 1).
///
/// Cada instância sobe um container próprio. Sem isso, todas as classes
/// compartilhariam o mesmo banco, e a aplicação de migrations falharia na
/// segunda instância.
///
/// <b>Por que PostgreSQL:</b> é o banco de referência do ambiente real.
/// Testar em SQLite mascararia violações de chave estrangeira, diferenças
/// de tradução de consultas e semântica de texto.
/// </summary>
public sealed class IsolatedApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("servia_api_test")
        .WithUsername("servia")
        .WithPassword("servia_test_password")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        // O Program.cs exige a connection string na inicializacao.
        // O valor real e substituido abaixo pelo container de teste.
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            _container.GetConnectionString());

        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(descriptor => descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_container.GetConnectionString()));
        });
    }
}
