using Adesha.Domain.Primitives;

namespace Adesha.Brokers.Abstractions.Models;

/// <summary>
/// State carried between the broker login initiation and completion steps. The state is
/// returned by <see cref="IBrokerAdapter.InitiateLoginAsync"/> and supplied to the
/// <c>Complete*</c> methods. This keeps the adapter stateless across HTTP requests, so
/// the multi-step broker login flow works even though the adapter is resolved per request.
/// </summary>
public sealed class BrokerLoginState
{
    /// <summary>The broker user id / login username.</summary>
    public required string Username { get; init; }

    /// <summary>
    /// Broker-specific handle required for the second step. For m.Stock TypeA this is
    /// null; for m.Stock TypeB this is the refreshToken from the login response.
    /// </summary>
    public string? RefreshHandle { get; init; }

    /// <summary>Identifier of the broker this state belongs to.</summary>
    public required BrokerId BrokerId { get; init; }
}
