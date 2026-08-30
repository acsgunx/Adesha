using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

public sealed class CanonicalFunds
{
    public required Money AvailableBalance { get; init; }
    public required Money UtilizedAmount { get; init; }
    public required Money ClearBalance { get; init; }
    public required Money Collateral { get; init; }
}
