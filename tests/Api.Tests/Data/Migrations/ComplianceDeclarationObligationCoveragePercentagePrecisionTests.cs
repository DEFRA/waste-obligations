using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;

namespace Defra.WasteObligations.Api.Tests.Data.Migrations;

public class ComplianceDeclarationObligationCoveragePercentagePrecisionTests
{
    [Fact]
    public void Name_ShouldDescribeMigration()
    {
        new ComplianceDeclarationObligationCoveragePercentagePrecision()
            .Name.Should()
            .Be("005 - ComplianceDeclaration obligation coverage percentage precision");
    }
}
