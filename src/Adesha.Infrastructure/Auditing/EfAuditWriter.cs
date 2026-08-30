using Adesha.Application.Auditing;
using Adesha.Domain.Auditing;
using Adesha.Infrastructure.Persistence;

namespace Adesha.Infrastructure.Auditing;

public sealed class EfAuditWriter(AdeshaDbContext dbContext) : IAuditWriter
{
    public async Task AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        dbContext.AuditRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
