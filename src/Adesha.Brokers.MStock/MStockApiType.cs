namespace Adesha.Brokers.MStock;

/// <summary>
/// m.Stock exposes two parallel API surfaces:
/// <list type="bullet">
/// <item><term>TypeA</term><description>Form-urlencoded auth, <c>Authorization: token api_key:jwtToken</c>.</description></item>
/// <item><term>TypeB</term><description>JSON auth, <c>Authorization: Bearer jwtToken</c> + <c>X-PrivateKey: api_key</c>.</description></item>
/// </list>
/// Selected via <c>MStock:ApiType</c> configuration.
/// </summary>
public enum MStockApiType
{
    TypeA,
    TypeB,
}
