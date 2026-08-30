using Adesha.Domain.Primitives;

namespace Adesha.Domain.Tests;

public class InstrumentIdTests
{
    [Fact]
    public void Empty_guid_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new InstrumentId(Guid.Empty));
    }

    [Fact]
    public void New_generates_unique_ids()
    {
        Assert.NotEqual(InstrumentId.New(), InstrumentId.New());
    }
}
