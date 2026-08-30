namespace Adesha.Domain.Primitives;

/// <summary>
/// Monetary value with 4-decimal scale, matching numeric(18,4) in PostgreSQL.
/// Always decimal; never double/float (Master Prompt Rule 5).
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const string DefaultCurrency = "INR";
    private const int Scale = 4;

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        // Banker's rounding is deliberate: it is decimal.Round's default and avoids
        // cumulative bias when aggregating many small P&L legs.
        Amount = decimal.Round(amount, Scale, MidpointRounding.ToEven);
        Currency = currency;
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public bool IsZero => Amount == 0m;
    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor, money.Currency);

    public static Money operator *(Money money, Quantity quantity) => new(money.Amount * quantity.Value, money.Currency);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine money in different currencies: {left.Currency} and {right.Currency}.");
        }
    }

    public override string ToString() => $"{Amount:0.0000} {Currency}";
}
