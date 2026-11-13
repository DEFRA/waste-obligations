using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Defra.WasteObligations.Api.Tests.Services.WastePackaging;

public class WastePackagingServiceTests(WireMockContext context) : WireMockTestBase(context)
{
    private const string Path = "/packaging/2025/producer-material-totals-by-submitter";

    [Fact]
    public async Task Read_ShouldUseConfiguredAddressAndBasicCredentialsWithoutRequestContext()
    {
        WireMock
            .Given(Request.Create().UsingGet().WithPath(Path).WithHeader("Authorization", "Basic Y2xpZW50OnNlY3JldA=="))
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBody(
                        """
                        {"items":[{"submitterOrganisationId":"11111111-1111-1111-1111-111111111111","submitterType":"DirectRegistrant","producers":[{"producerOrganisationId":"22222222-2222-2222-2222-222222222222","events":[{"code":1,"action":"Joined","date":"2025-03-03"}],"materials":[{"materialCode":"PL","kilograms":10000}]}]}]}
                        """
                    )
                    .WithHeader("Content-Type", "application/json")
            );
        await using var services = Services();

        var result = await services
            .GetRequiredService<IWastePackagingService>()
            .Read(2025, TestContext.Current.CancellationToken);

        var item = result.Items.Should().ContainSingle().Which;
        item.SubmitterOrganisationId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        item.SubmitterType.Should().Be(ObligationSubmitterType.DirectRegistrant);
        var producer = item.Producers.Should().ContainSingle().Which;
        producer.ProducerOrganisationId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var packagingEvent = producer.Events.Should().ContainSingle().Which;
        packagingEvent.Code.Should().Be(1);
        packagingEvent.Action.Should().Be(EventAction.Joined);
        packagingEvent.Date.Should().Be(new DateOnly(2025, 3, 3));
        var material = producer.Materials.Should().ContainSingle().Which;
        material.MaterialCode.Should().Be("PL");
        material.Kilograms.Should().Be(10000);
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
    [InlineData("{\"items\":[{\"submitterOrganisationId\":\"11111111-1111-1111-1111-111111111111\"}]}")]
    [InlineData(
        "{\"items\":[{\"submitterOrganisationId\":\"invalid\",\"submitterType\":\"DirectRegistrant\",\"producers\":[]}]}"
    )]
    [InlineData(
        "{\"items\":[{\"submitterOrganisationId\":\"11111111-1111-1111-1111-111111111111\",\"submitterType\":\"Unknown\",\"producers\":[]}]}"
    )]
    public async Task Read_ShouldRejectUnreadableEnvelope(string body)
    {
        WireMock
            .Given(Request.Create().UsingGet().WithPath(Path))
            .RespondWith(
                Response.Create().WithStatusCode(200).WithBody(body).WithHeader("Content-Type", "application/json")
            );
        await using var services = Services();
        Func<Task> fetch = () =>
            services.GetRequiredService<IWastePackagingService>().Read(2025, TestContext.Current.CancellationToken);

        await fetch.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Read_ShouldPropagateUnsuccessfulResponse()
    {
        WireMock.Given(Request.Create().UsingGet().WithPath(Path)).RespondWith(Response.Create().WithStatusCode(503));
        await using var services = Services();
        Func<Task> fetch = () =>
            services.GetRequiredService<IWastePackagingService>().Read(2025, TestContext.Current.CancellationToken);

        await fetch
            .Should()
            .ThrowAsync<HttpRequestException>()
            .Where(x => x.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Read_ShouldHonourCancellation()
    {
        await using var services = Services();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Func<Task> fetch = () => services.GetRequiredService<IWastePackagingService>().Read(2025, cancellation.Token);

        await fetch.Should().ThrowAsync<OperationCanceledException>();
    }

    private ServiceProvider Services() =>
        new ServiceCollection()
            .AddSingleton<IConfiguration>(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["WastePackaging:BaseAddress"] = Context.BaseAddress,
                            ["WastePackaging:ClientId"] = "client",
                            ["WastePackaging:ClientSecret"] = "secret",
                        }
                    )
                    .Build()
            )
            .AddWastePackagingService(false)
            .BuildServiceProvider();
}
