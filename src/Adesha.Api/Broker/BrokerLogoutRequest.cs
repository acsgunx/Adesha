using FluentValidation;

namespace Adesha.Api.Broker;

public sealed class BrokerLogoutRequest
{
    public required string BrokerId { get; init; }
}

public sealed class BrokerLogoutRequestValidator : AbstractValidator<BrokerLogoutRequest>
{
    public BrokerLogoutRequestValidator()
    {
        RuleFor(r => r.BrokerId).NotEmpty();
    }
}
