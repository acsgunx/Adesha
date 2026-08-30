using Microsoft.AspNetCore.Identity;

namespace Adesha.Infrastructure.Identity;

/// <summary>
/// The application's own login identity (owner account). Entirely separate from broker
/// credentials, which are a different concept stored and named separately.
/// </summary>
public sealed class AdeshaUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
