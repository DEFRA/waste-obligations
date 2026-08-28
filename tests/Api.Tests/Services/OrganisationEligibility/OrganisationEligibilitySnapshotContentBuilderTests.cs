using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using OrganisationComplianceDeclarationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationComplianceDeclarationEligibility;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class OrganisationEligibilitySnapshotContentBuilderTests
{
    [Fact]
    public void Create_WhenInputOrderOrUnresolvedStateChanges_ShouldKeepFingerprint()
    {
        var directProducer = CreateRow(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var complianceScheme = CreateRow(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RegistrationType.ComplianceScheme
        );
        var first = OrganisationEligibilitySnapshotContentBuilder.Create([directProducer, complianceScheme]);
        var second = OrganisationEligibilitySnapshotContentBuilder.Create([complianceScheme, directProducer]);

        second.Fingerprint.Should().Be(first.Fingerprint);
        second.Rows.Should().BeInAscendingOrder(x => x.OrganisationId);
    }

    [Fact]
    public void Create_WhenReferenceIsResolved_ShouldMaterialiseItAndChangeFingerprint()
    {
        var row = CreateRow(Guid.NewGuid());

        var unresolved = OrganisationEligibilitySnapshotContentBuilder.Create([row]);
        var resolved = OrganisationEligibilitySnapshotContentBuilder.Create([
            row with
            {
                ReferenceNumber = "051829",
                ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
            },
        ]);

        resolved.Fingerprint.Should().NotBe(unresolved.Fingerprint);
        resolved
            .Rows.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ReferenceNumber = "051829",
                    ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                },
                options => options.ExcludingMissingMembers()
            );
    }

    [Fact]
    public void Create_WhenSourceRowIsUnresolved_ShouldKeepItUnresolved()
    {
        var row = CreateRow(Guid.NewGuid());

        var content = OrganisationEligibilitySnapshotContentBuilder.Create([row]);

        content.Rows.Single().ReferenceNumber.Should().BeNull();
        content
            .Rows.Single()
            .ReferenceNumberResolutionState.Should()
            .Be(OrganisationReferenceNumberResolutionState.Pending);
    }

    private static OrganisationComplianceDeclarationEligibilityEntity CreateRow(
        Guid organisationId,
        RegistrationType registrationType = RegistrationType.DirectProducer
    ) =>
        OrganisationComplianceDeclarationEligibilityFixture
            .Default(organisationId)
            .With(x => x.Generation, "g1")
            .With(x => x.RegistrationType, registrationType)
            .With(x => x.ReferenceNumber, (string?)null)
            .With(x => x.ReferenceNumberResolutionState, OrganisationReferenceNumberResolutionState.Pending)
            .With(x => x.SourceFingerprint, $"source-{organisationId:D}")
            .Create();
}
