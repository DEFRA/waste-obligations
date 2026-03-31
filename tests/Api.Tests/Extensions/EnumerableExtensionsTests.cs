using AwesomeAssertions;
using Defra.WasteObligations.Api.Extensions;

namespace Defra.WasteObligations.Api.Tests.Extensions;

public class EnumerableExtensionsTests
{
    [Fact]
    public void NotNull_ShouldBeExpected()
    {
        var data = new List<int?> { 1, 2, null, 3 };

        data.NotNull().Should().BeEquivalentTo([1, 2, 3]);
    }
}
