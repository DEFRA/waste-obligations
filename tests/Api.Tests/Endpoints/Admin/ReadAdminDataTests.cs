using System.Net;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Endpoints.Admin;
using Defra.WasteObligations.Api.Services.Admin;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using NSubstitute;
using AuditEvent = Defra.WasteObligations.AuditEvents.Entities.AuditEvent;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class ReadAdminDataTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IAdminDataService AdminDataService { get; } = Substitute.For<IAdminDataService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IAdminDataService>(_ => AdminDataService);
    }

    [Fact]
    public async Task WhenAdminReadsComplianceDeclarations_ShouldStreamStoredEntities()
    {
        var organisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        var complianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(organisationId)
            .With(x => x.Id, ObjectId.Parse("68bc00000000000000000004"))
            .With(
                x => x.Audit,
                [
                    new ReasonAuditEntry("Cancelled")
                    {
                        User = new User
                        {
                            Id = "user-id",
                            Name = "User Name",
                            Email = "user@example.com",
                        },
                        Timestamp = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                        Reason = "Duplicate",
                    },
                ]
            )
            .Create();
        AdminDataService
            .ReadComplianceDeclarations(organisationId, 2026, Arg.Any<CancellationToken>())
            .Returns(Stream(complianceDeclaration));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationComplianceDeclarations(
                organisationId,
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyStream(response);
    }

    [Fact]
    public async Task WhenAdminReadsAuditEvents_ShouldStreamBoundedEntities()
    {
        var auditEvent = AuditEventFixture
            .ComplianceDeclaration(sequence: 7)
            .With(x => x.After, new BsonDocument("id", ObjectId.Parse("68bc00000000000000000005")))
            .Create();
        AdminDataService
            .ReadAuditEvents(6, 2, "compliance_declaration", "entity-7", Arg.Any<CancellationToken>())
            .Returns(Stream(auditEvent));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.AuditEvents(
                EndpointQuery
                    .New.Where(EndpointFilter.Query("afterSequence", "6"))
                    .Where(EndpointFilter.Query("limit", "2"))
                    .Where(EndpointFilter.Query("entity", "compliance_declaration"))
                    .Where(EndpointFilter.Query("entityId", "entity-7"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyStream(response);
    }

    [Fact]
    public async Task WhenAdminReadsEligibilitySnapshot_ShouldReturnStoredEntity()
    {
        AdminDataService
            .ReadEligibilitySnapshot(Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationEligibilitySnapshot
                {
                    Id = OrganisationEligibilitySnapshot.SnapshotId,
                    ActiveGeneration = "generation",
                    ActiveContentFingerprint = "fingerprint",
                    ActiveRowCount = 2,
                    MaterialisedStateVersion = 3,
                }
            );
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationEligibilitySnapshot(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenAdminReadsEligibility_ShouldStreamStoredEntities()
    {
        var eligibility = OrganisationComplianceDeclarationEligibilityFixture
            .Default()
            .With(x => x.Id, ObjectId.Parse("68bc00000000000000000006"))
            .With(x => x.Generation, "retained")
            .With(x => x.ReferenceNumberResolutionState, OrganisationReferenceNumberResolutionState.Failed)
            .Create();
        AdminDataService
            .ReadEligibility(
                "retained",
                OrganisationReferenceNumberResolutionState.Failed,
                Arg.Any<CancellationToken>()
            )
            .Returns(Stream(eligibility));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationEligibility(
                EndpointQuery
                    .New.Where(EndpointFilter.Query("generation", "retained"))
                    .Where(EndpointFilter.Query("referenceResolutionState", "Failed"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyStream(response);
    }

    [Fact]
    public async Task WhenAdminReadsOrganisationObligationSummaries_ShouldStreamStoredEntities()
    {
        var organisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        var summary = Summary(organisationId, OrganisationObligationRefreshState.Ready);
        AdminDataService
            .ReadOrganisationObligationSummaries(organisationId, Arg.Any<CancellationToken>())
            .Returns(Stream(summary));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationObligationSummaries(organisationId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyStream(response);
    }

    [Fact]
    public async Task WhenAdminReadsFailedOrganisationObligationSummaries_ShouldStreamStoredEntities()
    {
        var summary = Summary(
            Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5"),
            OrganisationObligationRefreshState.Failed
        );
        AdminDataService
            .ReadFailedOrganisationObligationSummaries(2026, Arg.Any<CancellationToken>())
            .Returns(Stream(summary));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.FailedOrganisationObligationSummaries(2026),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyStream(response);
    }

    [Fact]
    public async Task WhenAdminReadsHistoricalBackfills_ShouldStreamStoredEntities()
    {
        var backfill = new OrganisationObligationHistoricalBackfill
        {
            ObligationYear = 2024,
            Targets =
            [
                new OrganisationObligationHistoricalBackfillTarget
                {
                    OrganisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5"),
                },
            ],
            RequestedAt = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 6, 8, 1, 0, DateTimeKind.Utc),
        };
        AdminDataService.ReadHistoricalBackfills(Arg.Any<CancellationToken>()).Returns(Stream(backfill));
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationObligationHistoricalBackfills(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyStream(response);
    }

    [Fact]
    public async Task WhenAdminReadsRequestPacingState_ShouldReturnStoredEntity()
    {
        AdminDataService
            .ReadRequestPacingState(Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationObligationRequestPacingState
                {
                    Id = OrganisationObligationRequestPacingState.StateId,
                    DesiredRequestsPerMinute = 10,
                    EffectiveRequestsPerMinute = 8,
                    RecentDownstreamFailurePercentage = 12.5,
                    RateAdjustment = 0.8,
                    IsUnderPressure = true,
                    Version = 3,
                    UpdatedAt = new DateTime(2026, 9, 6, 9, 0, 0, DateTimeKind.Utc),
                }
            );
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationObligationRequestPacingState(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenAdminReadsAuditEventCounter_ShouldReturnStoredEntity()
    {
        AdminDataService
            .ReadAuditEventCounter(Arg.Any<CancellationToken>())
            .Returns(new AuditEventCounter { Id = "audit_event", Sequence = 42 });
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.AuditEventCounter(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenAuditEventLimitIsInvalid_ShouldReturnBadRequest()
    {
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.AuditEvents(EndpointQuery.New.Where(EndpointFilter.Query("limit", "501"))),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenAdminReadsMissingRequestPacingState_ShouldBeNotFound()
    {
        AdminDataService
            .ReadRequestPacingState(Arg.Any<CancellationToken>())
            .Returns((OrganisationObligationRequestPacingState?)null);
        var client = CreateClient(testUser: TestUser.Admin);

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.OrganisationObligationRequestPacingState(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenReadWriteUserReadsAuditEvents_ShouldBeForbidden()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Admin.AuditEvents(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static OrganisationObligationSummary Summary(
        Guid organisationId,
        OrganisationObligationRefreshState refreshState
    ) =>
        new()
        {
            Id = ObjectId.Parse("68bc00000000000000000007"),
            OrganisationId = organisationId,
            ObligationYear = 2026,
            ObligationCount = 1,
            TotalAcceptedTonnage = 10,
            TotalObligatedTonnage = 20,
            LastAttemptedAt = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            NextRefreshAt = new DateTime(2026, 9, 6, 8, 1, 0, DateTimeKind.Utc),
            RequestedAt = new DateTime(2026, 9, 6, 7, 0, 0, DateTimeKind.Utc),
            Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
            RefreshState = refreshState,
        };

    private static async IAsyncEnumerable<T> Stream<T>(params T[] entities)
    {
        foreach (var entity in entities)
        {
            yield return entity;
        }
    }

    private static async Task VerifyStream(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.Should().Be(EntityStreamResult<object>.ContentType);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson($"[{content.Trim()}]");
    }
}
