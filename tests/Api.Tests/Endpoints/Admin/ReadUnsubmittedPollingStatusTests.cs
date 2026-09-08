using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadUnsubmittedPollingStatusTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedPollingStatusService PollingStatusService { get; } =
        Substitute.For<IUnsubmittedPollingStatusService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedPollingStatusService>(_ => PollingStatusService);
    }

    [Fact]
    public async Task WhenAdmin_ShouldReturnPollingStatus()
    {
        PollingStatusService.Get(Arg.Any<CancellationToken>()).Returns(Status());
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingStatus(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenReadWriteUser_ShouldBeForbidden()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingStatus(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task WhenUnauthenticated_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.UnsubmittedPollingStatus(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static UnsubmittedPollingStatus Status() =>
        new()
        {
            Eligibility = new OrganisationEligibilityPollingStatus
            {
                ActiveGeneration = "generation",
                ActiveContentFingerprint = "fingerprint",
                ActiveRowCount = 4245,
                ActiveGenerationPromotedAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                LastVerifiedAt = new DateTime(2026, 9, 6, 9, 1, 0, DateTimeKind.Utc),
                MaterialisedStateVersion = 17,
                RefreshPollingEnabled = true,
                RefreshPollIntervalSeconds = 1800,
                VisibleRowCount = 4000,
                HydrationEligibleOrganisationCount = 4245,
                ReferenceResolutionStates =
                [
                    new OrganisationReferenceResolutionStatus { State = "Resolved", Count = 4245 },
                    new OrganisationReferenceResolutionStatus { State = "Pending", Count = 0 },
                ],
                Lease = new PollingWorkerLeaseStatus
                {
                    IsHeld = true,
                    ExpiresAt = new DateTime(2026, 9, 6, 9, 5, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                    LastReleasedAt = new DateTime(2026, 9, 6, 8, 30, 0, DateTimeKind.Utc),
                },
            },
            ObligationHydration = new OrganisationObligationHydrationPollingStatus
            {
                PollingEnabled = true,
                PollIntervalSeconds = 60,
                BatchSize = 20,
                MaxConcurrentRequests = 10,
                MaxDownstreamRequestsPerMinute = 200,
                TotalMinimumFullRefreshMinutes = 21.225,
                RefreshIntervalSeconds = 1800,
                MaximumSummaryStalenessSeconds = 7200,
                Years =
                [
                    new OrganisationObligationHydrationYearStatus
                    {
                        ObligationYear = 2026,
                        ActiveSummaryCount = 4245,
                        DueSummaryCount = 10,
                        PendingSummaryCount = 10,
                        ReadySummaryCount = 4234,
                        FailedSummaryCount = 1,
                        NeverSuccessfullyReadSummaryCount = 10,
                        OldestDueAt = new DateTime(2026, 9, 6, 8, 45, 0, DateTimeKind.Utc),
                        OldestSuccessfulReadAt = new DateTime(2026, 9, 6, 8, 45, 0, DateTimeKind.Utc),
                        LatestSuccessfulReadAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                        MinimumFullRefreshMinutes = 21.225,
                    },
                ],
                Lease = new PollingWorkerLeaseStatus
                {
                    IsHeld = false,
                    LastReleasedAt = new DateTime(2026, 9, 6, 8, 59, 0, DateTimeKind.Utc),
                },
            },
        };
}
