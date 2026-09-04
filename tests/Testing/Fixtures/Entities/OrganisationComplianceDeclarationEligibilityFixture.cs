using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class OrganisationComplianceDeclarationEligibilityFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<OrganisationComplianceDeclarationEligibility> Eligibility()
    {
        return GetFixture().Build<OrganisationComplianceDeclarationEligibility>();
    }

    public static IPostprocessComposer<OrganisationComplianceDeclarationEligibility> Default(
        Guid? organisationId = null
    )
    {
        return Eligibility()
            .With(x => x.Id, ObjectId.GenerateNewId)
            .With(x => x.Generation, "generation")
            .With(x => x.OrganisationId, () => organisationId ?? Guid.NewGuid())
            .With(x => x.ObligationYear, 2026)
            .With(x => x.RegistrationType, RegistrationType.DirectProducer)
            .With(x => x.RegistrationStatus, OrganisationRegistrationStatus.Registered)
            .Without(x => x.BusinessCountry)
            .With(x => x.Name, "Example organisation")
            .Without(x => x.TradingName)
            .Without(x => x.CompaniesHouseNumber)
            .With(x => x.ReferenceNumber, "100001")
            .With(x => x.ReferenceNumberResolutionState, OrganisationReferenceNumberResolutionState.Resolved)
            .With(x => x.IsVisibleInUnsubmittedView, true)
            .With(x => x.RecyclingObligationsMet, (bool?)null)
            .With(x => x.ObligationCoveragePercentage, (decimal?)null)
            .With(x => x.DeclarationStateUpdatedAt, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .With(x => x.SourceFingerprint, "source-fingerprint")
            .With(x => x.RefreshedAt, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
