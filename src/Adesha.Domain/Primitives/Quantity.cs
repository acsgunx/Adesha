namespace Adesha.Domain.Primitives;

/// <summary>
/// A strictly positive whole number of units (shares or lots).
/// </summary>
public readonly record struct Quantity : IComparable<Quantity>
{
    public long Value { get; }

    public Quantity(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Quantity must be a positive whole number.");
        }

        Value = value;
    }

    public static Quantity operator +(Quantity left, Quantity right) => new(left.Value + right.Value);

    public static Quantity operator -(Quantity left, Quantity right)
    {
        var result = left.Value - right.Value;
        return result <= 0
            ? throw new InvalidOperationException($"Subtracting {right.Value} from {left.Value} does not leave a positive quantity.")
            : new Quantity(result);
    }

    public static bool operator <(Quantity left, Quantity right) => left.Value < right.Value;
    public static bool operator >(Quantity left, Quantity right) => left.Value > right.Value;
    public static bool operator <=(Quantity left, Quantity right) => left.Value <= right.Value;
    public static bool operator >=(Quantity left, Quantity right) => left.Value >= right.Value;

    public int CompareTo(Quantity other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
