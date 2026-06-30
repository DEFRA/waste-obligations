using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using MongoDB.Bson;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;
using ComplianceDeclarationEntity = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class DeleteComplianceDeclarationTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenCreatedAndDeleted_ShouldRemoveFromMongoCollection()
    {
        var organisationId = Guid.NewGuid();
        using var sqsClient = CreateSqsClient();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );

        var client = CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<ComplianceDeclaration>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        created.Should().NotBeNull();
        await AssertAnalyticsEventQueued(sqsClient, created.Id, "insert", "submission.created");

        var filter = Builders<ComplianceDeclarationEntity>.Filter.Eq(x => x.Id, ObjectId.Parse(created.Id));
        var createdCount = await ComplianceDeclarations.CountDocumentsAsync(
            filter,
            cancellationToken: TestContext.Current.CancellationToken
        );

        createdCount.Should().Be(1);

        var deleteResponse = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(created.Id),
            TestContext.Current.CancellationToken
        );

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deletedCount = await ComplianceDeclarations.CountDocumentsAsync(
            filter,
            cancellationToken: TestContext.Current.CancellationToken
        );

        deletedCount.Should().Be(0);
        await AssertAnalyticsEventQueued(
            sqsClient,
            created.Id,
            "delete",
            "submission.removed",
            "System allowed endpoint access to delete"
        );
    }
}
