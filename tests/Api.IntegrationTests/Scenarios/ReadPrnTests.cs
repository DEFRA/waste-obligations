using System.Net;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class ReadPrnTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenOrganisationAndPrnFound_ResponseShouldContainMappedPrn()
    {
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.PrnCommonBackend
        );
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        var prn = PrnDetailsFixture.Default().With(x => x.OrganisationId, organisationId).Create();
        await WireMockContext.WireMockAdminApi.StubPrnCommonBackendPrnRequest(
            prn.ExternalId,
            prn,
            organisationId.ToString("D"),
            OAuth2Extensions.AccessToken
        );

        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Prns.Read(organisationId, prn.ExternalId.ToString("D")),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<Prn>(
            responseBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
        result.Should().BeEquivalentTo(prn.ToDto());

        using var responseJson = JsonDocument.Parse(responseBody);
        var root = responseJson.RootElement;
        root.GetProperty("issuedAt").GetString().Should().Be("2025-06-15T10:30:00+00:00");
        root.GetProperty("recipient").GetProperty("name").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("recipient").GetProperty("tradingName").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("recipient").GetProperty("registrationType").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("audit").GetProperty("createdAt").GetString().Should().Be("2026-01-15T10:00:00+00:00");
        root.GetProperty("audit").GetProperty("updatedAt").GetString().Should().Be("2026-01-15T10:05:00+00:00");
        root.GetProperty("audit").GetProperty("acceptedAt").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("audit").GetProperty("rejectedAt").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("audit").GetProperty("cancelledAt").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
