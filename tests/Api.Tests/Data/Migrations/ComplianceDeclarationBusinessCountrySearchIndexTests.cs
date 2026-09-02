using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;

namespace Defra.WasteObligations.Api.Tests.Data.Migrations;

public class ComplianceDeclarationBusinessCountrySearchIndexTests
{
    [Fact]
    public void Name_ShouldDescribeMigration()
    {
        new ComplianceDeclarationBusinessCountrySearchIndex()
            .Name.Should()
            .Be("009 - ComplianceDeclaration business country search index");
    }
}
