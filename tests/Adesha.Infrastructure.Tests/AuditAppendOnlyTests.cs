using Adesha.Domain.Auditing;
using Adesha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Adesha.Infrastructure.Tests;

/// <summary>
/// Rule 6: audit rows are append-only. These tests prove that update and delete are
/// impossible through the application's EF Core paths (change tracker + interceptor).
/// Known gap, addressed in Work Order 6: raw SQL / ExecuteUpdateAsync bypasses the
/// change tracker — database-level REVOKE will close that hole.
/// </summary>
public sealed class AuditAppendOnlyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AdeshaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AdeshaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new AppendOnlyAuditInterceptor())
            .Options;
        return new AdeshaDbContext(options);
    }

    private static AuditRecord NewRecord() => new()
    {
        Actor = "test-user",
        Action = "Order.Placed",
        EntityType = "Order",
        EntityId = Guid.NewGuid().ToString(),
        AfterState = """{"status":"PendingAtBroker"}""",
        CorrelationId = Guid.NewGuid().ToString("N"),
    };

    [Fact]
    public async Task Appending_an_audit_record_succeeds()
    {
        await using var context = CreateContext();
        var record = NewRecord();
        context.AuditRecords.Add(record);
        await context.SaveChangesAsync();

        Assert.True(record.Id > 0);
    }

    [Fact]
    public async Task Updating_an_audit_record_throws_and_persists_nothing()
    {
        await using (var context = CreateContext())
        {
            context.AuditRecords.Add(NewRecord());
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var record = await context.AuditRecords.OrderByDescending(a => a.Id).FirstAsync();
            context.Entry(record).Property(a => a.Actor).CurrentValue = "tampered";
            context.Entry(record).State = EntityState.Modified;

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        await using (var context = CreateContext())
        {
            var record = await context.AuditRecords.OrderByDescending(a => a.Id).FirstAsync();
            Assert.Equal("test-user", record.Actor);
        }
    }

    [Fact]
    public async Task Deleting_an_audit_record_throws_and_persists_nothing()
    {
        long id;
        await using (var context = CreateContext())
        {
            var record = NewRecord();
            context.AuditRecords.Add(record);
            await context.SaveChangesAsync();
            id = record.Id;
        }

        await using (var context = CreateContext())
        {
            var record = await context.AuditRecords.SingleAsync(a => a.Id == id);
            context.AuditRecords.Remove(record);

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }

        await using (var context = CreateContext())
        {
            Assert.NotNull(await context.AuditRecords.SingleOrDefaultAsync(a => a.Id == id));
        }
    }
}
