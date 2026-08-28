using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Defra.WasteObligations.Api.Tests.Services.WasteOrganisations;

public class WasteOrganisationsServiceTests : WireMockTestBase
{
    private const string TraceHeaderName = "x-cdp-request-id";
    private const string TraceId = "trace-id";

    private ServiceCollection Services { get; }

    public WasteOrganisationsServiceTests(WireMockContext context)
        : base(context)
    {
        var config = new Dictionary<string, string?>
        {
            { $"{WasteOrganisationsOptions.SectionName}:BaseAddress", context.BaseAddress },
            { $"{WasteOrganisationsOptions.SectionName}:ClientId", "client_id" },
            { $"{WasteOrganisationsOptions.SectionName}:ClientSecret", "client_secret" },
            { $"{WasteOrganisationsOptions.SectionName}:TotalRequestTimeout:Timeout", "00:00:40" },
            { $"{WasteOrganisationsOptions.SectionName}:AttemptTimeout:Timeout", "00:00:05" },
        };

        Services = [];
        Services.AddSingleton(new HeaderPropagationValues { Headers = new Dictionary<string, StringValues>() });
        Services.AddHeaderPropagation(options => options.Headers.Add(TraceHeaderName));
        Services.AddWasteOrganisationsService();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
    }

    [Fact]
    public async Task RequiredService_ShouldNotBeNull()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetService<IWasteOrganisationsService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task Read_ShouldReturnData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IWasteOrganisationsService>();

        WireMock.StubWasteOrganisationsOrganisationRequest(
            OrganisationFixture.OrganisationId,
            basicAuthToken: BasicAuthCredential.Default
        );

        var organisation = await service.Read(
            OrganisationFixture.OrganisationId,
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
    }

    [Fact]
    public async Task Read_ShouldPropagateTraceHeader()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IWasteOrganisationsService>();
        sp.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>
        {
            [TraceHeaderName] = TraceId,
        };

        WireMock.StubWasteOrganisationsOrganisationRequest(
            OrganisationFixture.OrganisationId,
            basicAuthToken: BasicAuthCredential.Default
        );

        await service.Read(OrganisationFixture.OrganisationId, TestContext.Current.CancellationToken);

        var request = WireMock
            .LogEntries.Single(x => x.RequestMessage?.Path == $"/organisations/{OrganisationFixture.OrganisationId:D}")
            .RequestMessage;
        request.Should().NotBeNull();
        request!.Headers.Should().ContainKey(TraceHeaderName).WhoseValue.Should().Contain(TraceId);
    }

    [Fact]
    public async Task OrganisationEligibilitySource_ShouldNotPropagateTraceHeader()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IOrganisationEligibilitySource>();
        sp.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>
        {
            [TraceHeaderName] = TraceId,
        };

        WireMock.StubWasteOrganisationsSearchRequest(basicAuthToken: BasicAuthCredential.Default);

        await service.Search(TestContext.Current.CancellationToken);

        var request = WireMock.LogEntries.Single(x => x.RequestMessage?.Path == "/organisations").RequestMessage;
        request.Should().NotBeNull();
        request!.Headers.Should().NotContainKey(TraceHeaderName);
    }

    [Fact]
    public async Task WhenNotFound_ShouldReturnNull()
    {
        var subject = new WasteOrganisationsService(Context.HttpClient);

        var result = await subject.Read(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Search_ShouldReturnUnfilteredOrganisationData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IWasteOrganisationsService>();
        WireMock.StubWasteOrganisationsSearchRequest(basicAuthToken: BasicAuthCredential.Default);

        var result = await service.Search(TestContext.Current.CancellationToken);

        result.Organisations.Should().ContainSingle();
        result.Organisations[0].Registrations.Should().NotBeEmpty();
    }
}
