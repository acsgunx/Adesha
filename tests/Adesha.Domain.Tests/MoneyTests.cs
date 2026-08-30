using Adesha.Domain.Primitives;

namespace Adesha.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Rounds_to_four_decimals_with_bankers_rounding()
    {
        Assert.Equal(1.2346m, new Money(1.23456m).Amount);
        Assert.Equal(1.2344m, new Money(1.23445m).Amount); // midpoint rounds to even
        Assert.Equal(1.2346m, new Money(1.23455m).Amount);
    }

    [Fact]
    public void Defaults_to_INR()
    {
        Assert.Equal("INR", new Money(10m).Currency);
    }

    [Fact]
    public void Addition_and_subtraction_work_for_same_currency()
    {
        var a = new Money(100.50m);
        var b = new Money(0.25m);
        Assert.Equal(new Money(100.75m), a + b);
        Assert.Equal(new Money(100.25m), a - b);
    }

    [Fact]
    public void Mixing_currencies_throws()
    {
        var inr = new Money(10m, "INR");
        var usd = new Money(10m, "USD");
        Assert.Throws<InvalidOperationException>(() => inr + usd);
        Assert.Throws<InvalidOperationException>(() => inr - usd);
        Assert.Throws<InvalidOperationException>(() => inr.CompareTo(usd));
    }

    [Fact]
    public void Multiplication_by_quantity_is_exact_decimal_arithmetic()
    {
        var price = new Money(1234.5678m);
        var qty = new Quantity(3);
        Assert.Equal(new Money(3703.7034m), price * qty);
    }

    [Fact]
    public void Comparison_operators_work()
    {
        Assert.True(new Money(1m) < new Money(2m));
        Assert.True(new Money(2m) >= new Money(2m));
        Assert.True(new Money(-1m).IsNegative);
        Assert.True(Money.Zero().IsZero);
    }

    [Fact]
    public void Empty_currency_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, ""));
        Assert.Throws<ArgumentException>(() => new Money(1m, "  "));
    }

    [Fact]
    public void Negation_negates_amount()
    {
        Assert.Equal(new Money(-5.5m), -new Money(5.5m));
    }
}
