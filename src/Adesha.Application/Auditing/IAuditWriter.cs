using Adesha.Domain.Auditing;

namespace Adesha.Application.Auditing;

/// <summary>
/// Port for appending audit records. There is deliberately no read-modify or delete
/// surface here: the audit log is append-only (Rule 6).
/// </summary>
public interface IAuditWriter
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken);
}
