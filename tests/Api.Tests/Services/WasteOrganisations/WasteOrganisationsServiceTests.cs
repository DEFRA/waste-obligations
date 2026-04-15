using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;

namespace Defra.WasteObligations.Api.Tests.Services.WasteOrganisations;

public class WasteOrganisationsServiceTests : WireMockTestBase
{
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
        Services.AddWasteOrganisationsService();
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config).Build());
        Services.TryAddSingleton<HeaderPropagationValues>();
    }

    [Fact]
    public async Task RequiredService_ShouldNotBeNull()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetService<IWasteOrganisationsService>();

        service.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadOrganisation_ShouldReturnData()
    {
        await using var sp = Services.BuildServiceProvider();

        var service = sp.GetRequiredService<IWasteOrganisationsService>();
        sp.GetRequiredService<HeaderPropagationValues>().Headers = new Dictionary<string, StringValues>();

        WireMock.StubWasteOrganisationsOrganisationRequest(
            OrganisationFixture.OrganisationId,
            basicAuthToken: BasicAuthCredential.Default
        );

        var organisation = await service.ReadOrganisation(
            OrganisationFixture.OrganisationId,
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenNotFound_ShouldReturnNull()
    {
        var subject = new WasteOrganisationsService(Context.HttpClient);

        var result = await subject.ReadOrganisation(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }
}
