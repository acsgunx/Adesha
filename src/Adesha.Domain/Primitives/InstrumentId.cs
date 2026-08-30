namespace Adesha.Domain.Primitives;

/// <summary>
/// Canonical internal identifier for a tradable instrument. Broker-specific symbols
/// map to this id at the adapter boundary; business logic never keys on a broker's
/// raw tradingsymbol (Master Prompt Rule 10 / instrument-master constraint).
/// </summary>
public readonly record struct InstrumentId
{
    public Guid Value { get; }

    public InstrumentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("InstrumentId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static InstrumentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("D");
}
