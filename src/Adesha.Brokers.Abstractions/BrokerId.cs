namespace Adesha.Brokers.Abstractions;

/// <summary>
/// Identifies which broker an adapter talks to. Adding a broker adds a value here;
/// nothing else in Domain or Application changes.
/// </summary>
public enum BrokerId
{
    MStock = 1,
    Zerodha = 2,
}
