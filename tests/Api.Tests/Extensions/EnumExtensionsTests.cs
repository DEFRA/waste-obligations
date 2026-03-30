using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Extensions;

namespace Defra.WasteObligations.Api.Tests.Extensions;

public class EnumExtensionsTests
{
    [Fact]
    public void ToJsonString_WhenNoAttribute_ShouldFallbackToValue()
    {
        FixtureType.Value1.ToJsonValue().Should().Be("Value1");
    }

    [Fact]
    public void FromJsonValue_WhenInvalid_ShouldThrow()
    {
        var act = () => "INVALID".FromJsonValue<FixtureType>();

        act.Should().Throw<JsonException>();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum FixtureType
    {
        Value1,
    }
}
