using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Tests.Dtos;

public class MappersTests
{
    [Fact]
    public void WhenUnknownStatus_ShouldThrow()
    {
        var act = () => ((ComplianceDeclarationStatus?)999).Value.ToEntity();

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be("Unknown status");
    }

    [Fact]
    public void WhenStatusIsKnown_ShouldMap()
    {
        foreach (var name in Enum.GetNames<ComplianceDeclarationStatus>())
        {
            Enum.Parse<ComplianceDeclarationStatus>(name)
                .ToEntity()
                .Should()
                .Be(Enum.Parse<Api.Data.Entities.ComplianceDeclarationStatus>(name));
        }
    }
}
