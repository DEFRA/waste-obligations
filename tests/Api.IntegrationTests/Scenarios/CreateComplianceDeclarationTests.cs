using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class CreateComplianceDeclarationTests : IntegrationTestBase
{
    private const string Analytics = "analytics";

    [Fact]
    public async Task WhenOrganisationFound_ShouldBeCreated()
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

        var complianceDeclaration = await client.GetFromJsonAsync<ComplianceDeclaration>(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Read(organisationId, result.Id),
            TestContext.Current.CancellationToken
        );

        result.Should().BeEquivalentTo(complianceDeclaration);

        await AsyncWaiter.WaitForAsync(async () =>
        {
            var entries = await WireMockContext.WireMockAdminApi.GetGovukNotifySendEmail();

            entries.Should().ContainSingle();

            var entry = entries[0];
            var jsonDocument = JsonDocument.Parse(entry.Request!.Body!);

            jsonDocument.RootElement.GetProperty("email_address").GetString().Should().Be("submitter@email.com");
        });

        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var auditEvent = await AuditEvents
                    .Find(x => x.EntityId == result.Id)
                    .SingleAsync(TestContext.Current.CancellationToken);

                auditEvent.Dispatches.Should().ContainKey(Analytics);
                auditEvent.Dispatches[Analytics].Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            },
            delay: TimeSpan.FromMilliseconds(100)
        );
    }
}
