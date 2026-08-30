namespace Adesha.Domain.Trading;

/// <summary>
/// Master Prompt Rule 2: every environment carries a TradingMode. The default
/// everywhere is Disabled; Live requires an explicit operator override.
/// </summary>
public enum TradingMode
{
    Disabled = 0,
    Paper = 1,
    Live = 2,
}
