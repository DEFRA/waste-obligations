using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;

namespace Defra.WasteObligations.Api.Tests.Data.Migrations;

public class ComplianceDeclarationBusinessCountryTests
{
    [Fact]
    public void Name_ShouldDescribeMigration()
    {
        new ComplianceDeclarationBusinessCountry().Name.Should().Be("008 - ComplianceDeclaration business country");
    }
}
