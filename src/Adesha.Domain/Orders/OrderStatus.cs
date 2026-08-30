namespace Adesha.Domain.Orders;

public enum OrderStatus
{
    /// <summary>Created locally; nothing has been sent to a broker.</summary>
    Created = 0,

    /// <summary>Submitted to the broker; no conclusive acknowledgement yet.</summary>
    PendingAtBroker = 1,

    /// <summary>Accepted by the broker/exchange and working.</summary>
    Open = 2,

    /// <summary>Some quantity filled; the remainder is still working.</summary>
    PartiallyFilled = 3,

    /// <summary>Entire quantity filled. Terminal.</summary>
    Filled = 4,

    /// <summary>Cancelled; any unfilled remainder will not execute. Terminal.</summary>
    Cancelled = 5,

    /// <summary>Rejected pre-trade or by the broker/exchange. Terminal.</summary>
    Rejected = 6,

    /// <summary>State could not be determined (timeout, ambiguous response).
    /// Must be resolved by reconciliation against the broker; never guessed.</summary>
    Unknown = 7,
}
