using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationObligations;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationObligations;

public class OrganisationObligationHydrationServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IDbContext _dbContext = Substitute.For<IDbContext>();
    private readonly RecordingLogger<OrganisationObligationHydrationService> _logger = new();

    [Theory]
    [InlineData(4999, 5, LogLevel.Debug)]
    [InlineData(5000, 5, LogLevel.Debug)]
    [InlineData(5001, 5, LogLevel.Warning)]
    [InlineData(6000, 10, LogLevel.Debug)]
    public async Task PrepareDueWork_ShouldLogReconciliationDurationExcludingWorkloadCounts(
        int durationMilliseconds,
        int warningThresholdSeconds,
        LogLevel expectedLevel
    )
    {
        const int obligationYear = 2026;
        ConfigureReconciliation(durationMilliseconds);
        _dbContext
            .OrganisationObligationSummaries.CountDocumentsAsync(
                Arg.Any<FilterDefinition<OrganisationObligationSummary>>(),
                Arg.Any<CountOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                _timeProvider.Advance(TimeSpan.FromSeconds(30));

                return 1L;
            });
        var subject = CreateSubject(warningThresholdSeconds);

        var result = await subject.PrepareDueWork(obligationYear, TestContext.Current.CancellationToken);

        result.ActiveSummaryCount.Should().Be(1);
        result.DueSummaryCount.Should().Be(1);
        _logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    Level = expectedLevel,
                    Message = $"Organisation obligation reconciliation took {durationMilliseconds}ms for 1 organisations in obligation year {obligationYear}"
                        + (expectedLevel == LogLevel.Warning ? $", exceeding {warningThresholdSeconds * 1000}ms" : ""),
                }
            );
    }

    [Theory]
    [InlineData(1000, LogLevel.Information)]
    [InlineData(5000, LogLevel.Information)]
    [InlineData(6000, LogLevel.Warning)]
    public async Task PrepareDueWork_WhenNormalLogLevelIsInformation_ShouldKeepSlowReconciliationsAtWarning(
        int durationMilliseconds,
        LogLevel expectedLevel
    )
    {
        ConfigureReconciliation(durationMilliseconds);
        var subject = CreateSubject(reconciliationLogLevel: LogLevel.Information);

        await subject.PrepareDueWork(2026, TestContext.Current.CancellationToken);

        _logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    Level = expectedLevel,
                    Message = $"Organisation obligation reconciliation took {durationMilliseconds}ms for 1 organisations in obligation year 2026"
                        + (expectedLevel == LogLevel.Warning ? ", exceeding 5000ms" : ""),
                }
            );
    }

    [Fact]
    public async Task PrepareDueWork_WhenReconciliationFails_ShouldPropagateWithoutLoggingDuration()
    {
        ConfigureReconciliation(6000, fail: true);
        var subject = CreateSubject();

        var act = () => subject.PrepareDueWork(2026, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Reconciliation failed");
        _logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnqueueNewEligible_ShouldOnlySubmitUpsertsForMissingSummaries(bool allSummariesExist)
    {
        var existingOrganisationId = Guid.NewGuid();
        var newOrganisationId = Guid.NewGuid();
        ConfigureReconciliation(
            0,
            eligibleOrganisationIds: [existingOrganisationId, newOrganisationId],
            existingOrganisationIds: allSummariesExist
                ? [existingOrganisationId, newOrganisationId]
                : [existingOrganisationId]
        );
        var subject = CreateSubject();

        await subject.EnqueueNewEligible(2026, TestContext.Current.CancellationToken);

        if (allSummariesExist)
        {
            await _dbContext
                .OrganisationObligationSummaries.DidNotReceive()
                .BulkWriteAsync(
                    Arg.Any<IEnumerable<WriteModel<OrganisationObligationSummary>>>(),
                    Arg.Any<BulkWriteOptions>(),
                    Arg.Any<CancellationToken>()
                );
        }
        else
        {
            await _dbContext
                .OrganisationObligationSummaries.Received(1)
                .BulkWriteAsync(
                    Arg.Is<IEnumerable<WriteModel<OrganisationObligationSummary>>>(x => x.Count() == 1),
                    Arg.Any<BulkWriteOptions>(),
                    Arg.Any<CancellationToken>()
                );
        }
    }

    private void ConfigureReconciliation(
        int durationMilliseconds,
        bool fail = false,
        Guid[]? eligibleOrganisationIds = null,
        Guid[]? existingOrganisationIds = null
    )
    {
        var snapshotCursor = Cursor(
            new OrganisationEligibilitySnapshot
            {
                Id = OrganisationEligibilitySnapshot.SnapshotId,
                ActiveGeneration = "active",
            }
        );
        _dbContext
            .OrganisationEligibilitySnapshots.FindAsync(
                Arg.Any<FilterDefinition<OrganisationEligibilitySnapshot>>(),
                Arg.Any<FindOptions<OrganisationEligibilitySnapshot, OrganisationEligibilitySnapshot>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(snapshotCursor);
        var organisationCursor = Cursor(eligibleOrganisationIds ?? [Guid.NewGuid()]);
        _dbContext
            .OrganisationComplianceDeclarationEligibilities.FindAsync(
                Arg.Any<FilterDefinition<OrganisationComplianceDeclarationEligibility>>(),
                Arg.Any<FindOptions<OrganisationComplianceDeclarationEligibility, Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(organisationCursor);
        var existingOrganisationCursor = Cursor(existingOrganisationIds ?? []);
        _dbContext
            .OrganisationObligationSummaries.FindAsync(
                Arg.Any<FilterDefinition<OrganisationObligationSummary>>(),
                Arg.Any<FindOptions<OrganisationObligationSummary, Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(existingOrganisationCursor);
        _dbContext
            .OrganisationObligationSummaries.UpdateManyAsync(
                Arg.Any<FilterDefinition<OrganisationObligationSummary>>(),
                Arg.Any<UpdateDefinition<OrganisationObligationSummary>>(),
                Arg.Any<UpdateOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new UpdateResult.Acknowledged(0, 0, null));
        _dbContext
            .OrganisationObligationSummaries.BulkWriteAsync(
                Arg.Any<IEnumerable<WriteModel<OrganisationObligationSummary>>>(),
                Arg.Any<BulkWriteOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                _timeProvider.Advance(TimeSpan.FromMilliseconds(durationMilliseconds));
                if (fail)
                    throw new InvalidOperationException("Reconciliation failed");

                return new BulkWriteResult<OrganisationObligationSummary>.Acknowledged(1, 0, 0, 0, 0, [], []);
            });
    }

    private static IAsyncCursor<T> Cursor<T>(params T[] items)
    {
        var cursor = Substitute.For<IAsyncCursor<T>>();
        cursor.Current.Returns(items);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(true, false);

        return cursor;
    }

    private OrganisationObligationHydrationService CreateSubject(
        int warningThresholdSeconds = 5,
        LogLevel reconciliationLogLevel = LogLevel.Debug
    ) =>
        new(
            _dbContext,
            Substitute.For<IOrganisationObligationSource>(),
            Substitute.For<IOrganisationObligationRequestPacer>(),
            Substitute.For<IOrganisationObligationHydrationMetrics>(),
            Options.Create(
                new OrganisationObligationHydrationOptions
                {
                    ReconciliationWarningThresholdSeconds = warningThresholdSeconds,
                    ReconciliationLogLevel = reconciliationLogLevel,
                }
            ),
            _timeProvider,
            _logger
        );
}
