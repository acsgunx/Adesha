using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Adesha.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` commands (migrations). The connection string is a
/// placeholder for schema generation only, or comes from ADESHA_MIGRATIONS_CONNECTION
/// when actually applying migrations (used by CI's migration-applies check).
/// </summary>
public sealed class AdeshaDbContextFactory : IDesignTimeDbContextFactory<AdeshaDbContext>
{
    public AdeshaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ADESHA_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Database=adesha;Username=adesha;Password=design-time-only";

        var options = new DbContextOptionsBuilder<AdeshaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AdeshaDbContext(options);
    }
}
