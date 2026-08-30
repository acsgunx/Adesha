using Adesha.Brokers.Abstractions.Errors;
using Adesha.Brokers.Abstractions.Models;

namespace Adesha.Brokers.Abstractions;

/// <summary>
/// The single broker abstraction. Every method takes a CancellationToken.
/// Brokers that lack a feature express it via <see cref="Capabilities"/>, not by
/// throwing NotImplementedException. Broker DTOs never leak past the adapter.
///
/// Work Order 2 implements READ operations only. Order mutation (PlaceOrderAsync,
/// ModifyOrderAsync, CancelOrderAsync) is Work Order 3 and is NOT in this interface yet.
/// </summary>
public interface IBrokerAdapter
{
    BrokerId BrokerId { get; }
    BrokerCapabilities Capabilities { get; }

    // --- Authentication ---

    /// <summary>
    /// m.Stock: username/password -> OTP sent to mobile. Returns a pending-login handle
    /// that the caller completes via <see cref="CompleteLoginWithOtpAsync"/>.
    /// Zerodha: not applicable (redirect flow) — the adapter throws
    /// <see cref="NotSupportedException"/> if called; use the broker-specific OAuth path.
    /// </summary>
    Task InitiateLoginAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Completes OTP-based login (m.Stock). Returns the authenticated session.
    /// </summary>
    Task<BrokerSession> CompleteLoginWithOtpAsync(string otp, CancellationToken cancellationToken);

    /// <summary>
    /// Completes TOTP-based login (m.Stock when TOTP is enabled).
    /// </summary>
    Task<BrokerSession> CompleteLoginWithTotpAsync(string totp, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the current session (restored from the session store on startup/reconnect).
    /// </summary>
    void SetSession(BrokerSession session);

    /// <summary>Invalidates the current session at the broker.</summary>
    Task LogoutAsync(CancellationToken cancellationToken);

    // --- Read operations (Work Order 2) ---

    Task<CanonicalFunds> GetFundsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Fetches the broker's instrument master (CSV for m.Stock) and maps to canonical
    /// instruments. This is a large payload; callers should cache the result.
    /// </summary>
    Task<IReadOnlyList<CanonicalInstrument>> GetInstrumentMasterAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LtpQuote>> GetLtpQuotesAsync(IEnumerable<string> exchangeSymbolPairs, CancellationToken cancellationToken);

    Task<IReadOnlyList<OhlcQuote>> GetOhlcQuotesAsync(IEnumerable<string> exchangeSymbolPairs, CancellationToken cancellationToken);

    Task<IReadOnlyList<CanonicalOrder>> GetOrderBookAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CanonicalTrade>> GetTradeBookAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CanonicalPosition>> GetPositionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CanonicalHolding>> GetHoldingsAsync(CancellationToken cancellationToken);
}
