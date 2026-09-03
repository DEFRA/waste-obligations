using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Extensions;

namespace Defra.WasteObligations.Api.Tests.Dtos;

public class BusinessCountryFilterTests
{
    [Theory]
    [InlineData(BusinessCountryFilter.England, "GB-ENG")]
    [InlineData(BusinessCountryFilter.NorthernIreland, "GB-NIR")]
    [InlineData(BusinessCountryFilter.Scotland, "GB-SCT")]
    [InlineData(BusinessCountryFilter.Wales, "GB-WLS")]
    public void ToJsonValue_ShouldMatchBusinessCountryStorageValue(BusinessCountryFilter country, string expected)
    {
        country.ToJsonValue().Should().Be(expected);
    }

    [Theory]
    [InlineData("GB-ENG", BusinessCountryFilter.England)]
    [InlineData("GB-NIR", BusinessCountryFilter.NorthernIreland)]
    [InlineData("GB-SCT", BusinessCountryFilter.Scotland)]
    [InlineData("GB-WLS", BusinessCountryFilter.Wales)]
    public void FromJsonValue_ShouldParseBusinessCountryStorageValue(string value, BusinessCountryFilter expected)
    {
        value.FromJsonValue<BusinessCountryFilter>().Should().Be(expected);
    }
}
