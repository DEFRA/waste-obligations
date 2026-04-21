using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class MappersTests
{
    [Fact]
    public void WhenUnknownStatus_ShouldThrow()
    {
        var act = () =>
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Status, (ComplianceDeclarationStatus)999)
                .Create()
                .ToDto();

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be("Unknown status");
    }
}
