using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Tests.Dtos;

public class ComplianceDeclarationSortParserTests
{
    [Fact]
    public void Parse_WhenNotSpecified_ShouldReturnEmpty()
    {
        var sort = ComplianceDeclarationSortParser.Parse(null);

        sort.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TryParse_WhenSortIsBlank_ShouldReturnFalse(string value)
    {
        var parsed = ComplianceDeclarationSortParser.TryParse(value, out var sort);

        parsed.Should().BeFalse();
        sort.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhenSortIsInvalid_ShouldThrow()
    {
        var act = () => ComplianceDeclarationSortParser.Parse("DateSubmitted[ascending]");

        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Parse_WhenSortIsValid_ShouldReturnFieldsInPriorityOrder()
    {
        var sort = ComplianceDeclarationSortParser.Parse("PercentageMet[asc],OrganisationId[desc],Regulation43[asc]");

        sort.Should()
            .Equal(
                new ComplianceDeclarationSort
                {
                    Field = ComplianceDeclarationSortField.PercentageMet,
                    Direction = ComplianceDeclarationSortDirection.Ascending,
                },
                new ComplianceDeclarationSort
                {
                    Field = ComplianceDeclarationSortField.OrganisationId,
                    Direction = ComplianceDeclarationSortDirection.Descending,
                },
                new ComplianceDeclarationSort
                {
                    Field = ComplianceDeclarationSortField.Regulation43,
                    Direction = ComplianceDeclarationSortDirection.Ascending,
                }
            );
    }
}
