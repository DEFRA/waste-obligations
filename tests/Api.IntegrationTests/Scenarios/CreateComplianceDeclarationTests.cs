using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using MongoDB.Driver;
using ComplianceDeclaration = Defra.WasteObligations.Api.Dtos.ComplianceDeclaration;
using WasteOrganisationsOrganisationFixture = Defra.WasteObligations.Testing.Fixtures.WasteOrganisations.OrganisationFixture;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class CreateComplianceDeclarationTests : IntegrationTestBase
{
    private const string Analytics = "analytics";

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

            AssertSubmittedEmail(entries[0].Request?.Body, "5f64e3bd-d454-4a45-a9c6-9409bf940d7a");
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

            AssertSubmittedEmail(entries[0].Request?.Body, "b103685d-de8a-4ea9-abc8-d244ca26841b");
        });
    }

    [Theory]
    [InlineData(true, "b3223b0b-a467-40c1-9150-f78b76d11fd8")]
    [InlineData(false, "95d8c2aa-a229-4ecb-9774-78ec06d0cbac")]
    public async Task WhenWelshOrganisation_ShouldSendWelshSubmittedEmail(
        bool directProducer,
        string expectedTemplateId
    )
    {
        var organisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsOrganisationRequest(
            organisationId,
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations),
            WasteOrganisationsOrganisationFixture
                .Default(organisationId)
                .With(x => x.BusinessCountry, BusinessCountry.Wales)
                .Create()
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(
            expiryInSeconds: 60,
            clientId: ClientIds.AccountBackend
        );

        var client = CreateClient();
        var request = directProducer
            ? CreateComplianceDeclarationRequestFixture.DirectProducer(organisationId).Create()
            : CreateComplianceDeclarationRequestFixture.ComplianceScheme(organisationId).Create();

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.ComplianceDeclarations.Create(organisationId),
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await AsyncWaiter.WaitForAsync(async () =>
        {
            var entries = await WireMockContext.WireMockAdminApi.GetGovukNotifySendEmail();

            entries.Should().ContainSingle();

            AssertSubmittedEmail(entries[0].Request?.Body, expectedTemplateId);
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
