using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;
using EntityRegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class UnsubmittedComplianceDeclarationStateProjectionTests : IntegrationTestBase
{
    [Fact]
    public async Task Search_WhenDeclarationIsCancelled_ShouldIncludeTheOrganisationAgain()
    {
        var organisationId = Guid.NewGuid();
        var generation = "generation";
        var verifiedAt = DateTime.UtcNow;
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = generation,
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 1,
                ActiveGenerationPromotedAt = verifiedAt,
                LastVerifiedAt = verifiedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await OrganisationEligibilities.InsertOneAsync(
            new OrganisationEligibility
            {
                Generation = generation,
                OrganisationId = organisationId,
                ObligationYear = 2026,
                RegistrationType = EntityRegistrationType.DirectProducer,
                RegistrationStatus = OrganisationRegistrationStatus.Registered,
                Name = "Alpha Packaging",
                ReferenceNumber = "100001",
                ReferenceNumberResolutionState = OrganisationReferenceNumberResolutionState.Resolved,
                SourceFingerprint = "source-fingerprint",
                RefreshedAt = verifiedAt,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await BackfillReviewState();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );
        await WireMockContext.WireMockAdminApi.StubAccountBackendOrganisationWithPersonsRequest(
            organisationId,
            OAuth2Extensions.AccessToken
        );
        var client = CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var declaration = await createResponse.Content.ReadFromJsonAsync<ComplianceDeclaration>(
            cancellationToken: TestContext.Current.CancellationToken
        );
        declaration.Should().NotBeNull();
        var submittedSearch = await Search(client);
        submittedSearch.Total.Should().Be(0);

        var cancelResponse = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(organisationId, declaration!.Id),
            UpdateComplianceDeclarationRequestFixture.Cancelled().Create(),
            TestContext.Current.CancellationToken
        );

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelledSearch = await Search(client);
        cancelledSearch.Total.Should().Be(1);
        cancelledSearch
            .UnsubmittedComplianceDeclarations.Should()
            .ContainSingle()
            .Which.OrganisationId.Should()
            .Be(organisationId);
        var reviewState = await ComplianceDeclarationReviewStates
            .Find(x =>
                x.OrganisationId == organisationId
                && x.ObligationYear == 2026
                && x.RegistrationType == EntityRegistrationType.DirectProducer
            )
            .SingleAsync(TestContext.Current.CancellationToken);
        reviewState.UnsubmittedExclusionCount.Should().Be(0);
    }

    private static async Task BackfillReviewState()
    {
        var dbContext = new MongoDbContext(
            GetMongoDatabase(),
            Options.Create(new MongoDbOptions()),
            NullLogger<MongoDbContext>.Instance
        );
        var result = await new ComplianceDeclarationReviewStateBackfillService(dbContext, TimeProvider.System).Backfill(
            TestContext.Current.CancellationToken
        );

        result.AlreadyComplete.Should().BeFalse();
    }

    private static async Task<UnsubmittedComplianceDeclarationsPaged> Search(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<UnsubmittedComplianceDeclarationsPaged>(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
            ),
            TestContext.Current.CancellationToken
        );

        response.Should().NotBeNull();
        return response!;
    }
}
