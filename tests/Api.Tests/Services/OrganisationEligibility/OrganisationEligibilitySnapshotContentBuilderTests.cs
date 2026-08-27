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
    public void Create_WhenInputOrderOrUnresolvedRetryStateChanges_ShouldKeepFingerprint()
    {
        var directProducer = CreateRow(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var complianceScheme = CreateRow(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RegistrationType.ComplianceScheme
        );
        var firstCaches = new[]
        {
            CreateCache(directProducer, OrganisationReferenceNumberResolutionState.Pending, referenceNumber: null),
            CreateCache(complianceScheme, OrganisationReferenceNumberResolutionState.NotFound, referenceNumber: null),
        };
        var secondCaches = new[]
        {
            CreateCache(complianceScheme, OrganisationReferenceNumberResolutionState.Failed, referenceNumber: null),
            CreateCache(directProducer, OrganisationReferenceNumberResolutionState.Pending, referenceNumber: null),
        };

        var first = OrganisationEligibilitySnapshotContentBuilder.Create(
            new[] { directProducer, complianceScheme },
            firstCaches
        );
        var second = OrganisationEligibilitySnapshotContentBuilder.Create(
            new[] { complianceScheme, directProducer },
            secondCaches
        );

        second.Fingerprint.Should().Be(first.Fingerprint);
        second.Rows.Should().BeInAscendingOrder(x => x.OrganisationId);
    }

    [Fact]
    public void Create_WhenReferenceIsResolved_ShouldMaterialiseItAndChangeFingerprint()
    {
        var row = CreateRow(Guid.NewGuid());

        var unresolved = OrganisationEligibilitySnapshotContentBuilder.Create(
            new[] { row },
            new[] { CreateCache(row, OrganisationReferenceNumberResolutionState.Pending, referenceNumber: null) }
        );
        var resolved = OrganisationEligibilitySnapshotContentBuilder.Create(
            new[] { row },
            new[] { CreateCache(row, OrganisationReferenceNumberResolutionState.Resolved, referenceNumber: "051829") }
        );

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
    public void Create_WhenReferenceCacheIsMissing_ShouldKeepTheSourceRowUnresolved()
    {
        var row = CreateRow(Guid.NewGuid());

        var content = OrganisationEligibilitySnapshotContentBuilder.Create(new[] { row }, []);

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
            .Without(x => x.ReferenceNumber)
            .With(x => x.ReferenceNumberResolutionState, OrganisationReferenceNumberResolutionState.Pending)
            .With(x => x.SourceFingerprint, $"source-{organisationId:D}")
            .Create();

    private static OrganisationReferenceCache CreateCache(
        OrganisationComplianceDeclarationEligibilityEntity row,
        OrganisationReferenceNumberResolutionState state,
        string? referenceNumber
    ) =>
        new()
        {
            OrganisationId = row.OrganisationId,
            RegistrationType = row.RegistrationType,
            LookupMode =
                row.RegistrationType == RegistrationType.DirectProducer
                    ? OrganisationReferenceLookupMode.AccountExternalId
                    : OrganisationReferenceLookupMode.CompaniesHouseNumber,
            ReferenceNumber = referenceNumber,
            ResolutionState = state,
        };
}
