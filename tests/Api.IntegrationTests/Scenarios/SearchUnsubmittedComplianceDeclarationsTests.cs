using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class SearchUnsubmittedComplianceDeclarationsTests : IntegrationTestBase
{
    [Fact]
    public async Task Search_WhenReady_ShouldReturnRegisteredResolvedOrganisationsWithoutActiveDeclaration()
    {
        var includedOrganisationId = Guid.NewGuid();
        var secondIncludedOrganisationId = Guid.NewGuid();
        var submittedOrganisationId = Guid.NewGuid();
        var generation = "generation";
        var verifiedAt = DateTime.UtcNow;
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = generation,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 4,
                ActiveGenerationPromotedAt = verifiedAt,
                LastVerifiedAt = verifiedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await ComplianceDeclarationReviewStateSnapshots.InsertOneAsync(
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = verifiedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationEligibilities.InsertManyAsync(
            [
                Eligibility(includedOrganisationId, generation, "Alpha Packaging", "100001"),
                Eligibility(secondIncludedOrganisationId, generation, "Zeta Packaging", "100004"),
                Eligibility(submittedOrganisationId, generation, "Beta Packaging", "100002"),
                Eligibility(Guid.NewGuid(), generation, "Cancelled Packaging", "100003") with
                {
                    RegistrationStatus = OrganisationRegistrationStatus.Cancelled,
                },
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );
        await ComplianceDeclarationReviewStates.InsertOneAsync(
            new ComplianceDeclarationReviewState
            {
                OrganisationId = submittedOrganisationId,
                ObligationYear = 2026,
                RegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType.DirectProducer,
                UnsubmittedExclusionCount = 1,
                UpdatedAt = verifiedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
                    .Where(EndpointFilter.Sort("OrganisationName[desc]"))
                    .Where(EndpointFilter.PageSize(1))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = await response.Content.ReadFromJsonAsync<UnsubmittedOrganisationsPaged>(
            TestContext.Current.CancellationToken
        );
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.UnsubmittedOrganisations.Should().ContainSingle();
        var row = result.UnsubmittedOrganisations.Single();
        row.OrganisationId.Should().Be(secondIncludedOrganisationId);
        row.OrganisationReferenceNumber.Should().Be("100004");
        row.ObligationCoveragePercentage.Should().Be(0);
        row.RecyclingObligationsMet.Should().BeNull();
        row.ObligationDataState.Should().Be("Pending");
        await VerifyJson(responseBody);
    }

    [Fact]
    public async Task Search_WhenActiveGenerationIsMissing_ShouldReturnAnEmptyPage()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UnsubmittedOrganisationsPaged>(
            TestContext.Current.CancellationToken
        );

        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.UnsubmittedOrganisations.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WhenEligibilityGenerationIsStale_ShouldReturnItsLastActiveGeneration()
    {
        var organisationId = Guid.NewGuid();
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = "stale-generation",
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 1,
                ActiveGenerationPromotedAt = DateTime.UtcNow.AddHours(-3),
                LastVerifiedAt = DateTime.UtcNow.AddHours(-3),
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationEligibilities.InsertOneAsync(
            Eligibility(organisationId, "stale-generation", "Alpha Packaging", "100001"),
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UnsubmittedOrganisationsPaged>(
            TestContext.Current.CancellationToken
        );

        result.Should().NotBeNull();
        result.Total.Should().Be(1);
        result.UnsubmittedOrganisations.Should().ContainSingle().Which.OrganisationId.Should().Be(organisationId);
    }

    private static OrganisationEligibility Eligibility(
        Guid organisationId,
        string generation,
        string name,
        string referenceNumber
    ) =>
        new()
        {
            Generation = generation,
            OrganisationId = organisationId,
            ObligationYear = 2026,
            RegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType.DirectProducer,
            RegistrationStatus = OrganisationRegistrationStatus.Registered,
            Name = name,
            ReferenceNumber = referenceNumber,
            ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
            SourceFingerprint = name,
            RefreshedAt = DateTime.UtcNow,
        };
}
