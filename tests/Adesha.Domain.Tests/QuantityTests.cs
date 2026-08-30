using Adesha.Domain.Primitives;

namespace Adesha.Domain.Tests;

public class QuantityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Non_positive_quantities_are_rejected(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(value));
    }

    [Fact]
    public void Addition_accumulates()
    {
        Assert.Equal(new Quantity(75), new Quantity(50) + new Quantity(25));
    }

    [Fact]
    public void Subtraction_below_one_throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Quantity(5) - new Quantity(5));
        Assert.Throws<InvalidOperationException>(() => new Quantity(5) - new Quantity(6));
    }

    [Fact]
    public void Comparisons_work()
    {
        Assert.True(new Quantity(1) < new Quantity(2));
        Assert.True(new Quantity(2) >= new Quantity(2));
    }
}
