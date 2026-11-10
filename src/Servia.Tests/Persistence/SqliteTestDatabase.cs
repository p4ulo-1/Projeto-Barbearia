using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Banco SQLite em memória para testes de persistência.
///
/// Mantém a conexão aberta durante toda a vida do contexto: o SQLite em
/// memória é descartado quando a última conexão fecha. Cada instância cria um
/// banco isolado, sem interferência entre testes.
///
/// Expõe o <see cref="TenantContext"/> para que os testes possam alternar
/// entre tenants e verificar o filtro global.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Contexto de tenant compartilhado pelos contextos criados.</summary>
    public TenantContext TenantContext { get; } = new();

    /// <summary>Cria um contexto novo sobre o mesmo banco em memória.</summary>
    public AppDbContext CreateContext() => CreateContext(TenantContext);

    /// <summary>
    /// Cria um contexto com um contexto de tenant específico.
    /// Permite simular requisições de tenants diferentes.
    /// </summary>
    public AppDbContext CreateContext(ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options, tenantContext);
    }

    public void Dispose() => _connection.Dispose();
}
