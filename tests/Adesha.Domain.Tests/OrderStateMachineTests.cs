using Adesha.Domain.Orders;

namespace Adesha.Domain.Tests;

public class OrderStateMachineTests
{
    /// <summary>
    /// The complete legal transition table. Everything NOT in this set must be rejected;
    /// the exhaustive test below checks every (from, to) pair against it.
    /// </summary>
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> Legal =
    [
        (OrderStatus.Created, OrderStatus.PendingAtBroker),
        (OrderStatus.Created, OrderStatus.Rejected),

        (OrderStatus.PendingAtBroker, OrderStatus.Open),
        (OrderStatus.PendingAtBroker, OrderStatus.PartiallyFilled),
        (OrderStatus.PendingAtBroker, OrderStatus.Filled),
        (OrderStatus.PendingAtBroker, OrderStatus.Cancelled),
        (OrderStatus.PendingAtBroker, OrderStatus.Rejected),
        (OrderStatus.PendingAtBroker, OrderStatus.Unknown),

        (OrderStatus.Open, OrderStatus.PartiallyFilled),
        (OrderStatus.Open, OrderStatus.Filled),
        (OrderStatus.Open, OrderStatus.Cancelled),
        (OrderStatus.Open, OrderStatus.Rejected),
        (OrderStatus.Open, OrderStatus.Unknown),

        (OrderStatus.PartiallyFilled, OrderStatus.PartiallyFilled),
        (OrderStatus.PartiallyFilled, OrderStatus.Filled),
        (OrderStatus.PartiallyFilled, OrderStatus.Cancelled),
        (OrderStatus.PartiallyFilled, OrderStatus.Unknown),

        (OrderStatus.Unknown, OrderStatus.PendingAtBroker),
        (OrderStatus.Unknown, OrderStatus.Open),
        (OrderStatus.Unknown, OrderStatus.PartiallyFilled),
        (OrderStatus.Unknown, OrderStatus.Filled),
        (OrderStatus.Unknown, OrderStatus.Cancelled),
        (OrderStatus.Unknown, OrderStatus.Rejected),
    ];

    public static TheoryData<OrderStatus, OrderStatus> AllPairs()
    {
        var data = new TheoryData<OrderStatus, OrderStatus>();
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Every_transition_matches_the_legal_table_exactly(OrderStatus from, OrderStatus to)
    {
        var expected = Legal.Contains((from, to));

        Assert.Equal(expected, OrderStateMachine.CanTransition(from, to));

        if (expected)
        {
            OrderStateMachine.EnsureTransition(from, to);
        }
        else
        {
            var ex = Assert.Throws<InvalidOrderTransitionException>(() => OrderStateMachine.EnsureTransition(from, to));
            Assert.Equal(from, ex.From);
            Assert.Equal(to, ex.To);
        }
    }

    [Theory]
    [InlineData(OrderStatus.Filled)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Rejected)]
    public void Terminal_states_have_no_exits(OrderStatus terminal)
    {
        Assert.True(OrderStateMachine.IsTerminal(terminal));
        Assert.Empty(OrderStateMachine.LegalTargets(terminal));
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.PendingAtBroker)]
    [InlineData(OrderStatus.Open)]
    [InlineData(OrderStatus.PartiallyFilled)]
    [InlineData(OrderStatus.Unknown)]
    public void Non_terminal_states_have_exits(OrderStatus state)
    {
        Assert.False(OrderStateMachine.IsTerminal(state));
        Assert.NotEmpty(OrderStateMachine.LegalTargets(state));
    }

    [Fact]
    public void Filled_is_never_reachable_directly_from_Created()
    {
        // "Never infer filled from placed successfully."
        Assert.False(OrderStateMachine.CanTransition(OrderStatus.Created, OrderStatus.Filled));
    }
}
