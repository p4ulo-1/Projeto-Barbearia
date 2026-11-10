using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Persistence;

/// <summary>
/// Banco SQLite em memória para testes de persistência.
///
/// Mantém a conexão aberta durante toda a vida do contexto: o SQLite em
/// memória é descartado quando a última conexão fecha. Cada instância cria um
/// banco isolado, sem interferência entre testes.
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

    /// <summary>Cria um contexto novo sobre o mesmo banco em memória.</summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
