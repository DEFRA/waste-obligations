using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class UpdateComplianceDeclarationTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenCreatedAndAccepted_ShouldUpdate()
    {
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );

        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ComplianceDeclaration>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.Should().NotBeNull();

        response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(organisationId, result.Id),
            UpdateComplianceDeclarationRequestFixture.Accepted().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var complianceDeclaration = await client.GetStringAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(organisationId, result.Id),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(complianceDeclaration).ScrubTopLevelIdMember().DisableDateCounting();
    }

    [Fact]
    public async Task WhenCreatedAndCancelled_ShouldUpdate()
    {
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );

        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ComplianceDeclaration>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        result.Should().NotBeNull();

        response = await client.PatchAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Update(organisationId, result.Id),
            UpdateComplianceDeclarationRequestFixture.Cancelled().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var complianceDeclaration = await client.GetStringAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(organisationId, result.Id),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(complianceDeclaration).ScrubTopLevelIdMember().DisableDateCounting();
    }
}
