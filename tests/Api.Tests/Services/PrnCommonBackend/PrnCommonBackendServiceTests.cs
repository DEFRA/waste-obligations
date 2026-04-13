using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;

namespace Defra.WasteObligations.Api.Tests.Services.PrnCommonBackend;

public class PrnCommonBackendServiceTests : WireMockTestBase
{
    private ServiceCollection Services { get; }

    public PrnCommonBackendServiceTests(WireMockContext context)
        : base(context)
    {
        var config = new Dictionary<string, string?>
        {
            { $"{PrnCommonBackendOptions.SectionName}:BaseAddress", context.BaseAddress },
            { $"{PrnCommonBackendOptions.SectionName}:TokenEndpoint", $"{context.BaseAddress}/token" },
            { $"{PrnCommonBackendOptions.SectionName}:ClientId", "client_id" },
            { $"{PrnCommonBackendOptions.SectionName}:ClientSecret", "client_secret" },
            { $"{PrnCommonBackendOptions.SectionName}:Scope", "scope" },
            { $"{PrnCommonBackendOptions.SectionName}:TotalRequestTimeout:Timeout", "00:00:40" },
            { $"{PrnCommonBackendOptions.SectionName}:AttemptTimeout:Timeout", "00:00:05" },
        };

        Services = [];
        Services.AddPrnCommonBackendService(true);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
        Services.TryAddSingleton<HeaderPropagationValues>();
        Services.AddTransient<ProxyHttpMessageHandler>();
    }

    [Fact]
    public async Task RequiredService_ShouldNotBeNull()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetService<IPrnCommonBackendService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadObligations_ShouldReturnData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IPrnCommonBackendService>();
        sp.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>();
        const int year = 2026;
        const string accessToken = "access_token";

        WireMock.StubTokenRequest();
        WireMock.StubPrnCommonBackendObligationsRequest(
            year,
            ObligationFixture.OrganisationId.ToString("D"),
            accessToken
        );

        var obligations = await service.ReadObligations(
            ObligationFixture.OrganisationId,
            year,
            TestContext.Current.CancellationToken
        );

        obligations.Should().NotBeNull();
        obligations.ObligationData.Should().ContainSingle();
    }

    [Fact]
    public async Task WhenNotFound_ShouldReturnNull()
    {
        var subject = new PrnCommonBackendService(Context.HttpClient);

        var result = await subject.ReadObligations(Guid.NewGuid(), 2026, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }
}
