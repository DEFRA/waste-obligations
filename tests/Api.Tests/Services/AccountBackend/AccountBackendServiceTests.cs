using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.AccountBackend;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Defra.WasteObligations.Api.Tests.Services.AccountBackend;

public class AccountBackendServiceTests : WireMockTestBase
{
    private const string TraceHeaderName = "x-cdp-request-id";
    private const string TraceId = "trace-id";

    private ServiceCollection Services { get; }

    public AccountBackendServiceTests(WireMockContext context)
        : base(context)
    {
        var config = new Dictionary<string, string?>
        {
            { $"{AccountBackendOptions.SectionName}:BaseAddress", context.BaseAddress },
            { $"{AccountBackendOptions.SectionName}:TokenEndpoint", $"{context.BaseAddress}/token" },
            { $"{AccountBackendOptions.SectionName}:ClientId", "client_id" },
            { $"{AccountBackendOptions.SectionName}:ClientSecret", "client_secret" },
            { $"{AccountBackendOptions.SectionName}:Scope", "scope" },
            { $"{AccountBackendOptions.SectionName}:TotalRequestTimeout:Timeout", "00:00:40" },
            { $"{AccountBackendOptions.SectionName}:AttemptTimeout:Timeout", "00:00:05" },
        };

        Services = [];
        Services.AddSingleton(new HeaderPropagationValues { Headers = new Dictionary<string, StringValues>() });
        Services.AddHeaderPropagation(options => options.Headers.Add(TraceHeaderName));
        Services.AddAccountBackendService();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
        Services.AddTransient<ProxyHttpMessageHandler>();
    }

    [Fact]
    public async Task RequiredService_ShouldNotBeNull()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetService<IAccountBackendService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadOrganisationWithPersons_ShouldReturnData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IAccountBackendService>();

        var organisationId = Guid.NewGuid();
        const string accessToken = "access_token";

        WireMock.StubTokenRequest();
        WireMock.StubAccountBackendOrganisationWithPersonsRequest(
            organisationId,
            accessToken,
            OrganisationWithPersonsFixture.CancellationRecipients()
        );

        var organisationWithPersons = await service.ReadOrganisationWithPersons(
            organisationId,
            TestContext.Current.CancellationToken
        );

        organisationWithPersons.Should().NotBeNull();
        organisationWithPersons
            .Persons.Should()
            .BeEquivalentTo(OrganisationWithPersonsFixture.CancellationRecipients().Persons);
    }

    [Fact]
    public async Task ReadOrganisationWithPersons_ShouldPropagateTraceHeader()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IAccountBackendService>();
        var organisationId = Guid.NewGuid();
        sp.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>
        {
            [TraceHeaderName] = TraceId,
        };

        WireMock.StubTokenRequest();
        WireMock.StubAccountBackendOrganisationWithPersonsRequest(organisationId);

        await service.ReadOrganisationWithPersons(organisationId, TestContext.Current.CancellationToken);

        var request = WireMock
            .LogEntries.Single(x =>
                x.RequestMessage?.Path == $"/api/organisations/organisation-with-persons/{organisationId:D}"
            )
            .RequestMessage;
        request.Should().NotBeNull();
        request!.Headers.Should().ContainKey(TraceHeaderName).WhoseValue.Should().Contain(TraceId);
    }

    [Fact]
    public async Task OrganisationReferenceSearch_ShouldNotPropagateTraceHeader()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IOrganisationReferenceSearchService>();
        var organisationId = Guid.NewGuid();
        sp.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>
        {
            [TraceHeaderName] = TraceId,
        };

        WireMock.StubTokenRequest();
        WireMock.StubAccountBackendOrganisationsByExternalIdsRequest();

        await service.SearchOrganisationsByExternalIds([organisationId], TestContext.Current.CancellationToken);

        var request = WireMock
            .LogEntries.Single(x => x.RequestMessage?.Path == "/api/organisations/organisations-by-externalIds")
            .RequestMessage;
        request.Should().NotBeNull();
        request!.Headers.Should().NotContainKey(TraceHeaderName);
    }

    [Fact]
    public async Task SearchOrganisationsByExternalIds_ShouldReturnData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IAccountBackendService>();

        var externalIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        const string accessToken = "access_token";
        var response = new OrganisationsByExternalIdsResponse
        {
            Organisations =
            [
                new AccountOrganisation { ExternalId = externalIds[0].ToString("D"), ReferenceNumber = "518293" },
            ],
            NotFoundExternalIds = [externalIds[1].ToString("D")],
        };

        WireMock.StubTokenRequest();
        WireMock.StubAccountBackendOrganisationsByExternalIdsRequest(accessToken, response);

        var result = await service.SearchOrganisationsByExternalIds(externalIds, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(response);

        var request = WireMock
            .LogEntries.Single(x => x.RequestMessage?.Path == "/api/organisations/organisations-by-externalIds")
            .RequestMessage;
        request.Should().NotBeNull();
        request!.Body.Should().Be($"{{\"externalIds\":[\"{externalIds[0]:D}\",\"{externalIds[1]:D}\"]}}");
    }

    [Fact]
    public async Task SearchOrganisationsByCompaniesHouseNumbers_ShouldReturnData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IAccountBackendService>();

        string[] companiesHouseNumbers = ["12345678", "87654321"];
        const string accessToken = "access_token";
        IReadOnlyList<AccountOrganisation> response =
        [
            new()
            {
                ExternalId = Guid.NewGuid().ToString("D"),
                ReferenceNumber = "530001",
                CompaniesHouseNumber = companiesHouseNumbers[0],
                IsComplianceScheme = true,
            },
        ];

        WireMock.StubTokenRequest();
        WireMock.StubAccountBackendOrganisationsByCompaniesHouseNumbersRequest(accessToken, response);

        var result = await service.SearchOrganisationsByCompaniesHouseNumbers(
            companiesHouseNumbers,
            TestContext.Current.CancellationToken
        );

        result.Should().BeEquivalentTo(response);

        var request = WireMock
            .LogEntries.Single(x =>
                x.RequestMessage?.Path == "/api/organisations/organisations-by-companies-house-numbers"
            )
            .RequestMessage;
        request.Should().NotBeNull();
        request!.Body.Should().Be("{\"companiesHouseNumbers\":[\"12345678\",\"87654321\"]}");
    }

    [Fact]
    public async Task ReadOrganisationWithPersons_WhenNotFound_ShouldReturnNull()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IAccountBackendService>();

        var organisationId = Guid.NewGuid();
        const string accessToken = "access_token";

        WireMock.StubTokenRequest();
        WireMock
            .Given(
                Request
                    .Create()
                    .UsingGet()
                    .WithPath($"/api/organisations/organisation-with-persons/{organisationId:D}")
                    .WithHeader("Authorization", $"Bearer {accessToken}")
            )
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.NotFound));

        var organisationWithPersons = await service.ReadOrganisationWithPersons(
            organisationId,
            TestContext.Current.CancellationToken
        );

        organisationWithPersons.Should().BeNull();
    }
}
