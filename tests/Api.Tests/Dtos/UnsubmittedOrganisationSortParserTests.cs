using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Tests.Dtos;

public class UnsubmittedOrganisationSortParserTests
{
    [Fact]
    public void Parse_WhenNotSpecified_ShouldReturnNull()
    {
        var sort = UnsubmittedOrganisationSortParser.Parse(null);

        sort.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("OrganisationName[ascending]")]
    [InlineData("OrganisationName[asc],PercentageMet[desc]")]
    [InlineData("DateSubmitted[asc]")]
    public void TryParse_WhenSortIsInvalid_ShouldReturnFalse(string value)
    {
        var parsed = UnsubmittedOrganisationSortParser.TryParse(value, out var sort);

        parsed.Should().BeFalse();
        sort.Should().BeNull();
    }

    [Theory]
    [InlineData("OrganisationName[asc]", UnsubmittedOrganisationSortField.OrganisationName)]
    [InlineData("OrganisationReferenceNumber[desc]", UnsubmittedOrganisationSortField.OrganisationReferenceNumber)]
    [InlineData("RecyclingObligations[asc]", UnsubmittedOrganisationSortField.RecyclingObligations)]
    [InlineData("PercentageMet[desc]", UnsubmittedOrganisationSortField.PercentageMet)]
    public void Parse_WhenSortIsValid_ShouldReturnUnsubmittedOrganisationSort(
        string value,
        UnsubmittedOrganisationSortField field
    )
    {
        var sort = UnsubmittedOrganisationSortParser.Parse(value);

        sort!
            .Should()
            .BeEquivalentTo(
                new UnsubmittedOrganisationSort
                {
                    Field = field,
                    Direction = value.EndsWith("[asc]")
                        ? UnsubmittedOrganisationSortDirection.Ascending
                        : UnsubmittedOrganisationSortDirection.Descending,
                }
            );
    }
}
