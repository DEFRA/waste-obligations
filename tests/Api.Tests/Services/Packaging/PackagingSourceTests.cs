using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.Packaging;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Defra.WasteObligations.Api.Tests.Services.Packaging;

public class PackagingSourceTests(WireMockContext context) : WireMockTestBase(context)
{
    private const string Path = "/packaging/2025/producer-material-totals-by-submitter";

    [Fact]
    public async Task GetFullYear_ShouldUseConfiguredAddressAndBasicCredentialsWithoutRequestContext()
    {
        WireMock
            .Given(Request.Create().UsingGet().WithPath(Path).WithHeader("Authorization", "Basic Y2xpZW50OnNlY3JldA=="))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBody(
                        """
                        {"items":[{"submitterOrganisationId":"11111111-1111-1111-1111-111111111111","submitterType":"DirectRegistrant","producers":[{"producerOrganisationId":"22222222-2222-2222-2222-222222222222","materials":[{"materialCode":"PL","kilograms":10000}]}]}]}
                        """
                    )
                    .WithHeader("Content-Type", "application/json")
            );
        await using var services = Services();

        var result = await services
            .GetRequiredService<IPackagingSource>()
            .GetFullYear(2025, TestContext.Current.CancellationToken);

        var item = result.Items.Should().ContainSingle().Which;
        item.GetProperty("submitterOrganisationId")
            .GetGuid()
            .Should()
            .Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        item.GetProperty("producers")[0]
            .GetProperty("materials")[0]
            .GetProperty("kilograms")
            .GetInt64()
            .Should()
            .Be(10000);
        var request = WireMock.LogEntries.Should().ContainSingle().Which.RequestMessage;
        request.Should().NotBeNull();
        request.Headers.Should().NotContainKey("x-cdp-request-id");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"items\":null}")]
    [InlineData("{\"items\":{}}")]
    [InlineData("not json")]
    public async Task GetFullYear_ShouldRejectUnreadableEnvelope(string body)
    {
        WireMock
            .Given(Request.Create().UsingGet().WithPath(Path))
            .RespondWith(
                Response.Create().WithStatusCode(200).WithBody(body).WithHeader("Content-Type", "application/json")
            );
        await using var services = Services();
        Func<Task> fetch = () =>
            services.GetRequiredService<IPackagingSource>().GetFullYear(2025, TestContext.Current.CancellationToken);

        await fetch.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetFullYear_ShouldPropagateUnsuccessfulResponse()
    {
        WireMock.Given(Request.Create().UsingGet().WithPath(Path)).RespondWith(Response.Create().WithStatusCode(503));
        await using var services = Services();
        Func<Task> fetch = () =>
            services.GetRequiredService<IPackagingSource>().GetFullYear(2025, TestContext.Current.CancellationToken);

        await fetch
            .Should()
            .ThrowAsync<HttpRequestException>()
            .Where(x => x.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetFullYear_ShouldHonourCancellation()
    {
        await using var services = Services();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> fetch = () => services.GetRequiredService<IPackagingSource>().GetFullYear(2025, cancellation.Token);

        await fetch.Should().ThrowAsync<OperationCanceledException>();
    }

    private ServiceProvider Services() =>
        new ServiceCollection()
            .AddSingleton<IConfiguration>(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Packaging:BaseAddress"] = Context.BaseAddress,
                            ["Packaging:ClientId"] = "client",
                            ["Packaging:ClientSecret"] = "secret",
                        }
                    )
                    .Build()
            )
            .AddPackagingService(false)
            .BuildServiceProvider();
}
