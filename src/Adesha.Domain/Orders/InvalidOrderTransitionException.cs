namespace Adesha.Domain.Orders;

public sealed class InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
    : InvalidOperationException($"Illegal order status transition: {from} -> {to}.")
{
    public OrderStatus From { get; } = from;
    public OrderStatus To { get; } = to;
}
