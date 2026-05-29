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

    [Fact]
    public void WhenStatusIsKnown_ShouldMap()
    {
        foreach (var name in Enum.GetNames<ComplianceDeclarationStatus>())
        {
            var declaration = ComplianceDeclarationFixture
                .Default()
                .With(x => x.Status, Enum.Parse<ComplianceDeclarationStatus>(name))
                .Create();

            declaration.ToDto().Status.Should().Be(Enum.Parse<Api.Dtos.ComplianceDeclarationStatus>(name));
        }
    }

    [Fact]
    public void WhenUnknownRegistrationType_ShouldThrow()
    {
        var act = () =>
            OrganisationFixture.Organisation().With(x => x.RegistrationType, (RegistrationType)999).Create().ToDto();

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be("Unknown registration type");
    }

    [Fact]
    public void WhenRegistrationTypeIsKnown_ShouldMap()
    {
        foreach (var name in Enum.GetNames<RegistrationType>())
        {
            var organisation = OrganisationFixture
                .Organisation()
                .With(x => x.RegistrationType, Enum.Parse<RegistrationType>(name))
                .Create();

            organisation.ToDto().RegistrationType.Should().Be(Enum.Parse<Api.Dtos.RegistrationType>(name));
        }
    }

    [Fact]
    public async Task WhenReasonAudit_ShouldMap()
    {
        var declaration = ComplianceDeclarationFixture
            .DirectProducer()
            .With(x => x.Audit, AuditEntryFixture.SubmittedThenCancelled())
            .Create()
            .ToDto();

        await Verify(declaration.Audit);
    }
}
