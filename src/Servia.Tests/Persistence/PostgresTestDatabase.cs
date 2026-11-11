using Microsoft.EntityFrameworkCore;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace Servia.Tests.Persistence;

/// <summary>
/// Banco PostgreSQL 17 isolado por classe de teste (Etapa 1).
///
/// Substitui o SQLite: o PostgreSQL é o banco de referência do ambiente
/// real, e testar contra ele elimina a classe de defeitos em que o teste
/// passa e a produção falha.
///
/// Cada instância sobe um container próprio. O container é descartado ao
/// final, então os testes são independentes e repetíveis.
///
/// <b>Por que não SQLite:</b> o SQLite ignora chaves estrangeiras por
/// padrão, não traduz comparações de <c>DateTimeOffset</c> e tem semântica
/// de texto diferente. Nenhuma dessas diferenças é aceitável em um teste
/// que precisa provar isolamento e integridade.
/// </summary>
public sealed class PostgresTestDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("servia_test")
        .WithUsername("servia")
        .WithPassword("servia_test_password")
        .Build();

    /// <summary>Contexto de tenant compartilhado pelos contextos criados.</summary>
    public TenantContext TenantContext { get; } = new();

    /// <summary>String de conexão do container em execução.</summary>
    public string ConnectionString => _container.GetConnectionString();

    private bool _initialized;

    public async Task InitializeAsync()
    {
        // O xUnit cria uma instancia da classe por teste, mas o container
        // e um recurso caro: sobe uma vez e e reutilizado.
        if (_initialized)
        {
            return;
        }

        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        _initialized = true;
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Cria um contexto sobre o banco do container.
    ///
    /// O <see cref="TenantContext"/> usa <c>AsyncLocal</c>: o valor definido
    /// em <c>InitializeAsync</c> nao flui para o metodo de teste, porque o
    /// xUnit executa cada um em contexto assincrono proprio. Por isso o
    /// tenant e reaplicado aqui, a cada criacao de contexto.
    /// </summary>
    public AppDbContext CreateContext()
    {
        if (DefaultTenantId.HasValue && !TenantContext.HasTenant)
        {
            TenantContext.SetTenant(DefaultTenantId.Value);
        }

        return CreateContext(TenantContext);
    }

    /// <summary>Tenant padrao aplicado quando nenhum foi definido.</summary>
    public Guid? DefaultTenantId { get; set; }

    /// <summary>
    /// Cria um contexto com um tenant específico.
    /// Permite simular requisições de tenants diferentes.
    /// </summary>
    public AppDbContext CreateContext(Servia.Domain.Tenancy.ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options, tenantContext);
    }
}
