using System.Runtime.CompilerServices;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.Admin;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using MongoDB.Bson;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class AdminDataServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Read_ShouldReturnStoredMongoEntitiesUsingIndexedQueries()
    {
        var firstOrganisationId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        var secondOrganisationId = Guid.Parse("ee6f35a2-8b1e-4b81-9208-a263b039156d");
        var earliest = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc);
        var later = earliest.AddMinutes(1);
        var olderComplianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(firstOrganisationId)
            .With(x => x.Id, ObjectId.Parse("68bc00000000000000000008"))
            .With(x => x.ObligationYear, 2025)
            .With(x => x.Created, earliest.AddDays(-2))
            .With(x => x.Updated, earliest)
            .Create();
        var newerComplianceDeclaration = ComplianceDeclarationFixture
            .DirectProducer(firstOrganisationId)
            .With(x => x.Id, ObjectId.Parse("68bc00000000000000000009"))
            .With(x => x.ObligationYear, 2026)
            .With(x => x.Created, earliest.AddDays(-1))
            .With(x => x.Updated, later)
            .Create();
        await ComplianceDeclarations.InsertManyAsync(
            [
                olderComplianceDeclaration,
                newerComplianceDeclaration,
                ComplianceDeclarationFixture.DirectProducer(secondOrganisationId).Create(),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        await AuditEvents.InsertManyAsync(
            [
                AuditEvent(1, "other", "other-1"),
                AuditEvent(2, "compliance_declaration", newerComplianceDeclaration.Id.ToString()),
                AuditEvent(3, "compliance_declaration", newerComplianceDeclaration.Id.ToString()),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        const string activeGeneration = "active";
        await OrganisationEligibilitySnapshots.InsertOneAsync(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = activeGeneration,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var failedEligibility = OrganisationComplianceDeclarationEligibilityFixture
            .Default(firstOrganisationId)
            .With(x => x.Generation, activeGeneration)
            .With(x => x.ReferenceNumberResolutionState, OrganisationReferenceNumberResolutionState.Failed)
            .Create();
        await OrganisationComplianceDeclarationEligibilities.InsertManyAsync(
            [
                failedEligibility,
                OrganisationComplianceDeclarationEligibilityFixture
                    .Default(secondOrganisationId)
                    .With(x => x.Generation, activeGeneration)
                    .Create(),
            ],
            cancellationToken: TestContext.Current.CancellationToken
        );

        var readySummary = Summary(firstOrganisationId, OrganisationObligationRefreshState.Ready, later);
        var failedSummary = Summary(secondOrganisationId, OrganisationObligationRefreshState.Failed, earliest);
        await OrganisationObligationSummaries.InsertManyAsync(
            [readySummary, failedSummary],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var historicalBackfill = new OrganisationObligationHistoricalBackfill
        {
            ObligationYear = 2024,
            Targets = [new OrganisationObligationHistoricalBackfillTarget { OrganisationId = firstOrganisationId }],
            RequestedAt = earliest,
            UpdatedAt = later,
        };
        await OrganisationObligationHistoricalBackfills.InsertOneAsync(
            historicalBackfill,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var requestPacingState = new OrganisationObligationRequestPacingState
        {
            Id = OrganisationObligationRequestPacingState.StateId,
            DesiredRequestsPerMinute = 10,
            EffectiveRequestsPerMinute = 8,
            RecentDownstreamFailurePercentage = 0,
            RateAdjustment = 1,
            IsUnderPressure = false,
            Version = 1,
            UpdatedAt = later,
        };
        await OrganisationObligationRequestPacingStates.InsertOneAsync(
            requestPacingState,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var workerLease = new BackgroundWorkerLease
        {
            Id = BackgroundWorkerLease.OrganisationEligibilityRefreshLeaseId,
            CreatedAt = earliest,
            UpdatedAt = later,
            ExpiresAt = later.AddMinutes(1),
        };
        await OrganisationWorkerLeases.InsertOneAsync(
            workerLease,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var auditEventCounter = new AuditEventCounter { Id = "audit_event", Sequence = 3 };
        await AuditEventCounters.InsertOneAsync(
            auditEventCounter,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var auditEventDispatchLease = new AuditEventDispatchLease
        {
            Id = "analytics",
            CreatedAt = earliest,
            UpdatedAt = later,
            ExpiresAt = later.AddMinutes(1),
        };
        await AuditEventDispatchLeases.InsertOneAsync(
            auditEventDispatchLease,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var migrationLease = new MongoMigrationLease
        {
            Id = "mongo-migrations",
            Owner = "owner",
            ExpiresAt = later.AddMinutes(1),
        };
        await MongoMigrationLeases.InsertOneAsync(
            migrationLease,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var subject = new AdminDataService(GetMongoApplicationDatabase());

        var complianceDeclarations = await ReadAll(
            subject.ReadComplianceDeclarations(firstOrganisationId, null, TestContext.Current.CancellationToken)
        );
        var auditEvents = await ReadAll(
            subject.ReadAuditEvents(
                1,
                10,
                "compliance_declaration",
                newerComplianceDeclaration.Id.ToString(),
                TestContext.Current.CancellationToken
            )
        );
        var eligibilitySnapshot = await subject.ReadEligibilitySnapshot(TestContext.Current.CancellationToken);
        var eligibility = await ReadAll(
            subject.ReadEligibility(
                null,
                OrganisationReferenceNumberResolutionState.Failed,
                TestContext.Current.CancellationToken
            )
        );
        var referenceResolutionIssues = await ReadAll(
            subject.ReadReferenceResolutionIssues(TestContext.Current.CancellationToken)
        );
        var organisationSummaries = await ReadAll(
            subject.ReadOrganisationObligationSummaries(firstOrganisationId, TestContext.Current.CancellationToken)
        );
        var failedSummaries = await ReadAll(
            subject.ReadFailedOrganisationObligationSummaries(2026, TestContext.Current.CancellationToken)
        );
        var historicalBackfills = await ReadAll(subject.ReadHistoricalBackfills(TestContext.Current.CancellationToken));
        var storedRequestPacingState = await subject.ReadRequestPacingState(TestContext.Current.CancellationToken);
        var workerLeases = await ReadAll(subject.ReadWorkerLeases(TestContext.Current.CancellationToken));
        var storedAuditEventCounter = await subject.ReadAuditEventCounter(TestContext.Current.CancellationToken);
        var storedAuditEventDispatchLease = await subject.ReadAuditEventDispatchLease(
            "analytics",
            TestContext.Current.CancellationToken
        );
        var storedMigrationLease = await subject.ReadMongoMigrationLease(TestContext.Current.CancellationToken);

        complianceDeclarations.Should().BeEquivalentTo([olderComplianceDeclaration, newerComplianceDeclaration]);
        auditEvents.Select(x => x.Sequence).Should().BeEquivalentTo([2L, 3L], options => options.WithStrictOrdering());
        eligibilitySnapshot
            .Should()
            .BeEquivalentTo(
                new { Id = OrganisationEligibilitySnapshot.SnapshotId, ActiveGeneration = activeGeneration }
            );
        eligibility.Should().BeEquivalentTo([failedEligibility]);
        referenceResolutionIssues.Should().BeEquivalentTo([failedEligibility]);
        organisationSummaries.Should().BeEquivalentTo([readySummary]);
        failedSummaries.Should().BeEquivalentTo([failedSummary]);
        historicalBackfills.Should().BeEquivalentTo([historicalBackfill]);
        storedRequestPacingState.Should().BeEquivalentTo(requestPacingState);
        workerLeases.Should().BeEquivalentTo([workerLease]);
        storedAuditEventCounter.Should().BeEquivalentTo(auditEventCounter);
        storedAuditEventDispatchLease.Should().BeEquivalentTo(auditEventDispatchLease);
        storedMigrationLease.Should().BeEquivalentTo(migrationLease);
    }

    private static AuditEvent AuditEvent(long sequence, string entity, string entityId) =>
        new()
        {
            EventId = $"event-{sequence}",
            Sequence = sequence,
            Entity = entity,
            EntityId = entityId,
            Operation = "insert",
            EventType = "submission.created",
            OccurredAt = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
            RecordedAt = new DateTime(2026, 9, 6, 8, 1, 0, DateTimeKind.Utc),
            Actor = "service:waste-obligations",
            Version = 1,
            SchemaVersion = "v1.3",
        };

    private static async Task<T[]> ReadAll<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var entity in source)
        {
            result.Add(entity);
        }

        return [.. result];
    }

    private static OrganisationObligationSummary Summary(
        Guid organisationId,
        OrganisationObligationRefreshState refreshState,
        DateTime nextRefreshAt
    ) =>
        new()
        {
            OrganisationId = organisationId,
            ObligationYear = 2026,
            LastAttemptedAt = nextRefreshAt.AddMinutes(-1),
            NextRefreshAt = nextRefreshAt,
            RequestedAt = nextRefreshAt.AddMinutes(-2),
            Priority = OrganisationObligationHydrationPriority.ScheduledRefresh,
            RefreshState = refreshState,
        };
}
