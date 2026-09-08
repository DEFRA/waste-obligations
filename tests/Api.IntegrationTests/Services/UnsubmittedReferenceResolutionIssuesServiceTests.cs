using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class UnsubmittedReferenceResolutionIssuesServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_ShouldReturnNonResolvedRowsFromTheActiveGeneration()
    {
        const string activeGeneration = "active";
        var notFoundOrganisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        var ambiguousOrganisationId = Guid.Parse("ee6f35a2-8b1e-4b81-9208-a263b039156d");
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = activeGeneration,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationComplianceDeclarationEligibilities.InsertManyAsync(
            [
                Eligibility(
                    activeGeneration,
                    notFoundOrganisationId,
                    OrganisationReferenceNumberResolutionState.NotFound,
                    RegistrationType.ComplianceScheme,
                    "Example scheme"
                ),
                Eligibility(
                    activeGeneration,
                    ambiguousOrganisationId,
                    OrganisationReferenceNumberResolutionState.Ambiguous,
                    RegistrationType.DirectProducer,
                    "Example producer"
                ),
                Eligibility(
                    activeGeneration,
                    Guid.NewGuid(),
                    OrganisationReferenceNumberResolutionState.Resolved,
                    RegistrationType.DirectProducer,
                    "Resolved organisation"
                ),
                Eligibility(
                    "retained",
                    Guid.NewGuid(),
                    OrganisationReferenceNumberResolutionState.NotFound,
                    RegistrationType.DirectProducer,
                    "Retained organisation"
                ),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = CreateSubject();

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result.ActiveGeneration.Should().Be(activeGeneration);
        result
            .ReferenceResolutionIssues.Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        OrganisationId = ambiguousOrganisationId,
                        ObligationYear = 2026,
                        RegistrationType = Defra.WasteObligations.Api.Dtos.RegistrationType.DirectProducer,
                        RegistrationStatus = "Registered",
                        Name = "Example producer",
                        TradingName = "Example trading name",
                        CompaniesHouseNumber = "01234567",
                        Country = "GB-ENG",
                        ReferenceResolutionState = "Ambiguous",
                    },
                    new
                    {
                        OrganisationId = notFoundOrganisationId,
                        ObligationYear = 2026,
                        RegistrationType = Defra.WasteObligations.Api.Dtos.RegistrationType.ComplianceScheme,
                        RegistrationStatus = "Registered",
                        Name = "Example scheme",
                        TradingName = "Example trading name",
                        CompaniesHouseNumber = "01234567",
                        Country = "GB-ENG",
                        ReferenceResolutionState = "NotFound",
                    },
                ],
                options => options.WithStrictOrdering()
            );
    }

    [Fact]
    public async Task Get_WhenThereIsNoActiveGeneration_ShouldReturnNoIssues()
    {
        var subject = CreateSubject();

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result.ActiveGeneration.Should().BeNull();
        result.ReferenceResolutionIssues.Should().BeEmpty();
    }

    private static UnsubmittedReferenceResolutionIssuesService CreateSubject() =>
        new(
            new MongoDbContext(
                GetMongoApplicationDatabase(),
                Options.Create(new MongoDbOptions()),
                NullLogger<MongoDbContext>.Instance
            )
        );

    private static OrganisationComplianceDeclarationEligibility Eligibility(
        string generation,
        Guid organisationId,
        OrganisationReferenceNumberResolutionState referenceResolutionState,
        RegistrationType registrationType,
        string name
    ) =>
        new()
        {
            Generation = generation,
            OrganisationId = organisationId,
            ObligationYear = 2026,
            RegistrationType = registrationType,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            BusinessCountry = "GB-ENG",
            Name = name,
            TradingName = "Example trading name",
            CompaniesHouseNumber = "01234567",
            ReferenceNumberResolutionState = referenceResolutionState,
            SourceFingerprint = "fingerprint",
        };
}
