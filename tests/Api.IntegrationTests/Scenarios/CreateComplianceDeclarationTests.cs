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
    private const string DirectProducerEnglishTemplateId = "5f64e3bd-d454-4a45-a9c6-9409bf940d7a";
    private const string ComplianceSchemeEnglishTemplateId = "b103685d-de8a-4ea9-abc8-d244ca26841b";

    [Fact]
    public async Task WhenOrganisationFound_ShouldBeCreated()
    {
        var organisationId = Guid.NewGuid();
        using var sqsClient = CreateSqsClient();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations)
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );

        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TraceHeaderName, TraceId);

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

            AssertSubmittedEmail(entries[0].Request?.Body, DirectProducerEnglishTemplateId);
        });

        await AsyncWaiter.WaitForAsync(
            async () =>
            {
                var auditEvent = await AuditEvents
                    .Find(x => x.EntityId == result.Id)
                    .SingleAsync(TestContext.Current.CancellationToken);

                auditEvent.TraceId.Should().Be(TraceId);
                auditEvent.Dispatches.Should().ContainKey(Analytics);
                auditEvent.Dispatches[Analytics].Status.Should().Be(AuditEventDispatchStatus.Dispatched);
                auditEvent.Dispatches[Analytics].Date.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            },
            delay: TimeSpan.FromMilliseconds(100)
        );

        await AssertAnalyticsEventQueued(sqsClient, result.Id, "insert", "submission.created");
    }

    [Fact]
    public async Task WhenComplianceScheme_ShouldSendComplianceSchemeSubmittedEmail()
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
            CreateComplianceDeclarationRequestFixture.ComplianceScheme(organisationId).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await AsyncWaiter.WaitForAsync(async () =>
        {
            var entries = await WireMockContext.WireMockAdminApi.GetGovukNotifySendEmail();

            entries.Should().ContainSingle();

            AssertSubmittedEmail(entries[0].Request?.Body, ComplianceSchemeEnglishTemplateId);
        });
    }

    private static void AssertSubmittedEmail(string? body, string expectedTemplateId)
    {
        if (body is null)
            throw new InvalidOperationException("Expected GOV.UK Notify request body.");

        using var jsonDocument = JsonDocument.Parse(body);
        var personalisation = jsonDocument.RootElement.GetProperty("personalisation");

        jsonDocument.RootElement.GetProperty("email_address").GetString().Should().Be("submitter@email.com");
        jsonDocument.RootElement.GetProperty("template_id").GetString().Should().Be(expectedTemplateId);
        personalisation.GetProperty("user").GetString().Should().Be("Submitter Name");
    }
}
