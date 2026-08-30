namespace Adesha.Infrastructure.Identity;

/// <summary>
/// Refresh token for the app's own session, stored as a SHA-256 hash only.
/// Rotation: each use revokes the token and records its replacement, so a replayed
/// old token is detectable and revokes the whole chain.
/// </summary>
public sealed class RefreshToken
{
    public long Id { get; init; }
    public required Guid UserId { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTimeOffset.UtcNow < ExpiresAtUtc;
}
