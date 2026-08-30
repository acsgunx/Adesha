using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Adesha.Application.Configuration;
using Adesha.Infrastructure.Identity;
using Adesha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Adesha.Api.Auth;

public sealed class TokenService(AdeshaDbContext dbContext, IOptions<AdeshaOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _jwt = options.Value.Jwt;

    public async Task<TokenPairResponse> IssueTokenPairAsync(AdeshaUser user, CancellationToken cancellationToken)
    {
        var (accessToken, expiresAt) = CreateAccessToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, cancellationToken);
        return new TokenPairResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = expiresAt,
            RefreshToken = refreshToken,
        };
    }

    /// <summary>
    /// Rotates a refresh token: the presented token is revoked and replaced. Presenting an
    /// already-revoked token is treated as replay and revokes every active token for the user.
    /// </summary>
    public async Task<TokenPairResponse?> RotateAsync(string presentedToken, CancellationToken cancellationToken)
    {
        var hash = Hash(presentedToken);
        var existing = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        if (existing.RevokedAtUtc is not null)
        {
            // Replay of a rotated token: assume compromise, kill the whole session family.
            await dbContext.RefreshTokens
                .Where(t => t.UserId == existing.UserId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
            return null;
        }

        if (now >= existing.ExpiresAtUtc)
        {
            return null;
        }

        var user = await dbContext.Users.SingleAsync(u => u.Id == existing.UserId, cancellationToken);
        var newRefreshToken = GenerateRefreshTokenValue();

        existing.RevokedAtUtc = now;
        existing.ReplacedByTokenHash = Hash(newRefreshToken);
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(newRefreshToken),
            ExpiresAtUtc = now.AddDays(_jwt.RefreshTokenLifetimeDays),
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAt) = CreateAccessToken(user);
        return new TokenPairResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = expiresAt,
            RefreshToken = newRefreshToken,
        };
    }

    public async Task RevokeAsync(string presentedToken, CancellationToken cancellationToken)
    {
        var hash = Hash(presentedToken);
        var now = timeProvider.GetUtcNow();
        await dbContext.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
    }

    private (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AdeshaUser user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_jwt.AccessTokenLifetimeMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("N")),
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var value = GenerateRefreshTokenValue();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(value),
            ExpiresAtUtc = timeProvider.GetUtcNow().AddDays(_jwt.RefreshTokenLifetimeDays),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return value;
    }

    private static string GenerateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
