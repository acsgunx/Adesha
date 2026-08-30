namespace Adesha.Domain.Orders;

/// <summary>
/// Encodes every legal order status transition. Anything not listed here is illegal
/// and must be rejected loudly — "filled" is never inferred from "placed successfully".
/// </summary>
public static class OrderStateMachine
{
    private static readonly IReadOnlyDictionary<OrderStatus, IReadOnlySet<OrderStatus>> LegalTransitions =
        new Dictionary<OrderStatus, IReadOnlySet<OrderStatus>>
        {
            [OrderStatus.Created] = new HashSet<OrderStatus>
            {
                // Rejected here covers pre-trade validation failures that never reach a broker.
                OrderStatus.PendingAtBroker,
                OrderStatus.Rejected,
            },
            [OrderStatus.PendingAtBroker] = new HashSet<OrderStatus>
            {
                OrderStatus.Open,
                OrderStatus.PartiallyFilled,
                OrderStatus.Filled,
                OrderStatus.Cancelled,
                OrderStatus.Rejected,
                OrderStatus.Unknown,
            },
            [OrderStatus.Open] = new HashSet<OrderStatus>
            {
                OrderStatus.PartiallyFilled,
                OrderStatus.Filled,
                OrderStatus.Cancelled,
                // Exchanges can reject a working order after acceptance (e.g. margin recheck).
                OrderStatus.Rejected,
                OrderStatus.Unknown,
            },
            [OrderStatus.PartiallyFilled] = new HashSet<OrderStatus>
            {
                // Self-transition: each additional partial fill is a legal update.
                OrderStatus.PartiallyFilled,
                OrderStatus.Filled,
                // Cancelling the unfilled remainder of a partially filled order.
                OrderStatus.Cancelled,
                OrderStatus.Unknown,
            },
            [OrderStatus.Unknown] = new HashSet<OrderStatus>
            {
                // Reconciliation against the broker resolves Unknown into the true state.
                OrderStatus.PendingAtBroker,
                OrderStatus.Open,
                OrderStatus.PartiallyFilled,
                OrderStatus.Filled,
                OrderStatus.Cancelled,
                OrderStatus.Rejected,
            },
            // Terminal states: no transitions out.
            [OrderStatus.Filled] = new HashSet<OrderStatus>(),
            [OrderStatus.Cancelled] = new HashSet<OrderStatus>(),
            [OrderStatus.Rejected] = new HashSet<OrderStatus>(),
        };

    public static bool IsTerminal(OrderStatus status) => LegalTransitions[status].Count == 0;

    public static bool CanTransition(OrderStatus from, OrderStatus to) => LegalTransitions[from].Contains(to);

    public static void EnsureTransition(OrderStatus from, OrderStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOrderTransitionException(from, to);
        }
    }

    public static IReadOnlySet<OrderStatus> LegalTargets(OrderStatus from) => LegalTransitions[from];
}
