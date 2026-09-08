using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;

namespace Defra.WasteObligations.Api.Tests.Data.Migrations;

public class OrganisationEligibilityBusinessCountrySearchIndexTests
{
    [Fact]
    public void Name_ShouldDescribeMigration()
    {
        new OrganisationEligibilityBusinessCountrySearchIndex()
            .Name.Should()
            .Be("013 - Organisation eligibility business country search index");
    }
}
