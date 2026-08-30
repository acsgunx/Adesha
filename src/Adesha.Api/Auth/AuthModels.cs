using System.Text.Json.Serialization;
using FluentValidation;

namespace Adesha.Api.Auth;

public sealed class SetupOwnerRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }
}

public sealed class SetupOwnerResponse
{
    [JsonPropertyName("sharedKey")]
    public required string SharedKey { get; init; }

    [JsonPropertyName("otpauthUri")]
    public required string OtpauthUri { get; init; }
}

public sealed class ConfirmTotpRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("totpCode")]
    public required string TotpCode { get; init; }
}

public sealed class LoginRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("totpCode")]
    public required string TotpCode { get; init; }
}

public sealed class TokenPairResponse
{
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("accessTokenExpiresAtUtc")]
    public required DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; init; }
}

public sealed class RefreshRequest
{
    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; init; }
}

public sealed class SetupOwnerRequestValidator : AbstractValidator<SetupOwnerRequest>
{
    public SetupOwnerRequestValidator()
    {
        RuleFor(r => r.Username).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Password).NotEmpty().MinimumLength(12).MaximumLength(256);
    }
}

public sealed class ConfirmTotpRequestValidator : AbstractValidator<ConfirmTotpRequest>
{
    public ConfirmTotpRequestValidator()
    {
        RuleFor(r => r.Username).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
        RuleFor(r => r.TotpCode).NotEmpty().Matches("^[0-9]{6}$");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Username).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
        RuleFor(r => r.TotpCode).NotEmpty().Matches("^[0-9]{6}$");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(r => r.RefreshToken).NotEmpty();
    }
}
