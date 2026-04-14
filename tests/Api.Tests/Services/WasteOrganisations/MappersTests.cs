using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;

namespace Defra.WasteObligations.Api.Tests.Services.WasteOrganisations;

public class MappersTests
{
    [Theory]
    [InlineData(BusinessCountry.England, "Environment Agency")]
    [InlineData(BusinessCountry.NorthernIreland, "Northern Ireland Environment Agency")]
    [InlineData(BusinessCountry.Scotland, "Scottish Environment Protection Agency")]
    [InlineData(BusinessCountry.Wales, "Natural Resources Wales")]
    [InlineData(null, null)]
    public void ToDto_ShouldMapRegulator(string? businessCountry, string? expectedRegulator)
    {
        var result = OrganisationFixture.Default().With(x => x.BusinessCountry, businessCountry).Create().ToDto();

        result.Regulator.Should().Be(expectedRegulator);
    }

    [Fact]
    public async Task ToDto_ShouldMap()
    {
        var result = OrganisationFixture.Default().Create().ToDto();

        await Verify(result).DontScrubGuids();
    }
}
