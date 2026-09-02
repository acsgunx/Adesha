using FluentValidation;

namespace Adesha.Api.Broker;

public sealed class BrokerInitiateLoginRequest
{
    public required string BrokerId { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed class BrokerCompleteOtpRequest
{
    public required string BrokerId { get; init; }
    public required string Otp { get; init; }
}

public sealed class BrokerCompleteTotpRequest
{
    public required string BrokerId { get; init; }
    public required string Totp { get; init; }
}

public sealed class BrokerInitiateLoginRequestValidator : AbstractValidator<BrokerInitiateLoginRequest>
{
    public BrokerInitiateLoginRequestValidator()
    {
        RuleFor(r => r.BrokerId).NotEmpty();
        RuleFor(r => r.Username).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}

public sealed class BrokerCompleteOtpRequestValidator : AbstractValidator<BrokerCompleteOtpRequest>
{
    public BrokerCompleteOtpRequestValidator()
    {
        RuleFor(r => r.BrokerId).NotEmpty();
        RuleFor(r => r.Otp).NotEmpty();
    }
}

public sealed class BrokerCompleteTotpRequestValidator : AbstractValidator<BrokerCompleteTotpRequest>
{
    public BrokerCompleteTotpRequestValidator()
    {
        RuleFor(r => r.BrokerId).NotEmpty();
        RuleFor(r => r.Totp).NotEmpty();
    }
}
