using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Organisations.Obligations;

public class ReadObligationsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private RecordingLogger<ReadObligationsLatency> LatencyLogger { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IWasteOrganisationsService>(_ => new FakeWasteOrganisationsService());
        services.AddTransient<IPrnCommonBackendService>(_ => new FakePrnCommonBackendService());
        services.AddSingleton<ILogger<ReadObligationsLatency>>(LatencyLogger);
    }

    [Fact]
    public async Task WhenFound_ShouldReturnObligations()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                FakeWasteOrganisationsService.OrganisationId,
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(FakeWasteOrganisationsService.Year))
            ),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
        await AsyncWaiter.WaitForAsync(
            () =>
            {
                LatencyLogger
                    .Messages.Should()
                    .ContainSingle(x => x.StartsWith("Read organisation obligations latency:"));

                return Task.CompletedTask;
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(25)
        );
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        LatencyLogger.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenWriteOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WhenInvalidYear_ShouldBeBadRequest()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Obligations.Read(
                Guid.NewGuid(),
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2022))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
