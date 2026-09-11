using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Data;

public class MongoMigrationServiceTests
{
    [Fact]
    public async Task Execute_WhenMigrationAttemptTimesOutAndStops_ShouldRetryWhileRetainingLease()
    {
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        leaseService.TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var firstAttemptCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempt = 0;
        migrationRunner
            .Run(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                attempt++;

                if (attempt == 2)
                    return Task.CompletedTask;

                var cancellationToken = callInfo.Arg<CancellationToken>();
                cancellationToken.Register(() =>
                {
                    firstAttemptCancelled.TrySetResult();
                    firstAttempt.TrySetCanceled(cancellationToken);
                });

                return firstAttempt.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(leaseService, migrationRunner, logger: logger);

        await subject.Execute(TestContext.Current.CancellationToken);
        await firstAttemptCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain(message => message.Contains("exceeded its 00:00:01 limit"));
        logger.Messages.Should().Contain(message => message.Contains("Retrying in 00:00:01"));
        await migrationRunner.Received(2).Run(Arg.Any<CancellationToken>());
        await leaseService.Received(1).TryAcquire(TimeSpan.FromSeconds(2), Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenTimedOutMigrationDoesNotStop_ShouldRenewLeaseAndNotRunAnotherAttempt()
    {
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var leaseRenewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        leaseService
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                leaseRenewed.TrySetResult();

                return Task.FromResult(true);
            });
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var migrationCancellationRequested = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var migrationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        migrationRunner
            .Run(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.Arg<CancellationToken>();
                cancellationToken.Register(() => migrationCancellationRequested.TrySetResult());

                return migrationCompletion.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(leaseService, migrationRunner, logger: logger);
        using var stopping = new CancellationTokenSource();
        var execution = subject.Execute(stopping.Token);

        await migrationCancellationRequested.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
        await leaseRenewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        await migrationRunner.Received(1).Run(Arg.Any<CancellationToken>());
        logger.Messages.Should().Contain(message => message.Contains("exclusive lease will be retained"));

        await stopping.CancelAsync();
        migrationCompletion.TrySetCanceled(TestContext.Current.CancellationToken);
        await execution;

        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenLeaseRenewalFails_ShouldCancelMigrationAndNotRetry()
    {
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        leaseService.TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var migrationCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var migrationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        migrationRunner
            .Run(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.Arg<CancellationToken>();
                cancellationToken.Register(() =>
                {
                    migrationCancelled.TrySetResult();
                    migrationCompletion.TrySetCanceled(cancellationToken);
                });

                return migrationCompletion.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(leaseService, migrationRunner, logger: logger);

        await subject.Execute(TestContext.Current.CancellationToken);
        await migrationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Mongo migration lease was not renewed. Cancelling the migration engine.");
        await migrationRunner.Received(1).Run(Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenLeaseRenewalThrowsAnUnexpectedCancellation_ShouldCancelMigrationAndNotRetry()
    {
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        leaseService
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
                Task.FromException<bool>(new OperationCanceledException("Mongo driver cancelled the operation."))
            );
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var migrationCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var migrationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        migrationRunner
            .Run(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.Arg<CancellationToken>();
                cancellationToken.Register(() =>
                {
                    migrationCancelled.TrySetResult();
                    migrationCompletion.TrySetCanceled(cancellationToken);
                });

                return migrationCompletion.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(leaseService, migrationRunner, logger: logger);

        await subject.Execute(TestContext.Current.CancellationToken);
        await migrationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Mongo migration lease renewal failed. Cancelling the migration engine.");
        await migrationRunner.Received(1).Run(Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenLeaseAcquisitionFails_ShouldLogTheFailureAndStopWhenTheHostStops()
    {
        using var stopping = new CancellationTokenSource();
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stopping.Cancel();

                return Task.FromException<bool>(new InvalidOperationException("MongoDB is unavailable."));
            });
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(leaseService, migrationRunner, logger: logger);

        await subject.Execute(stopping.Token);

        logger.Messages.Should().Contain("Mongo migration lease acquisition failed. Retrying.");
        await migrationRunner.DidNotReceive().Run(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenAnotherHostHoldsTheLease_ShouldWaitUntilTheHostStops()
    {
        using var stopping = new CancellationTokenSource();
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stopping.Cancel();

                return Task.FromResult(false);
            });
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(leaseService, migrationRunner, logger: logger);

        await subject.Execute(stopping.Token);

        await migrationRunner.DidNotReceive().Run(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenMigrationLeaseIsUnavailablePastAlertThreshold_ShouldLogAnError()
    {
        using var stopping = new CancellationTokenSource();
        var timeProvider = new FakeTimeProvider();
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                timeProvider.Advance(TimeSpan.FromSeconds(300));
                stopping.Cancel();

                return Task.FromResult(false);
            });
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(
            leaseService,
            migrationRunner,
            new MongoMigrationOptions
            {
                LeaseDurationSeconds = 2,
                LeaseRenewalIntervalSeconds = 1,
                AttemptTimeoutSeconds = 1,
                RetryDelaySeconds = 1,
                LeaseAcquisitionAlertThresholdSeconds = 300,
                MaximumAttempts = 1,
            },
            logger,
            timeProvider
        );

        var execution = subject.Execute(stopping.Token);
        await execution;

        logger
            .Entries.Should()
            .Contain(x =>
                x.Level == LogLevel.Error
                && x.Message.Contains("Mongo migration lease has not been acquired after 00:05:00")
            );
        await migrationRunner.DidNotReceive().Run(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenHostStopsDuringMigrationRetryDelay_ShouldReleaseLease()
    {
        using var stopping = new CancellationTokenSource();
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        migrationRunner.Run(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(
            leaseService,
            migrationRunner,
            new MongoMigrationOptions
            {
                LeaseDurationSeconds = 2,
                LeaseRenewalIntervalSeconds = 60,
                AttemptTimeoutSeconds = 1,
                RetryDelaySeconds = 60,
                MaximumAttempts = 2,
            },
            logger
        );
        var execution = subject.Execute(stopping.Token);

        await AsyncWaiter.WaitForAsync(
            () =>
            {
                logger.Messages.Should().Contain(message => message.Contains("Retrying in 00:01:00"));

                return Task.CompletedTask;
            },
            timeout: 5,
            delay: TimeSpan.FromMilliseconds(10)
        );
        await stopping.CancelAsync();
        await execution;

        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenMigrationFailsAndLeaseReleaseFails_ShouldLogBothFailures()
    {
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        leaseService
            .Release(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("MongoDB is unavailable.")));
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        migrationRunner
            .Run(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("The migration failed.")));
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(
            leaseService,
            migrationRunner,
            new MongoMigrationOptions
            {
                LeaseDurationSeconds = 2,
                LeaseRenewalIntervalSeconds = 1,
                AttemptTimeoutSeconds = 1,
                RetryDelaySeconds = 1,
                MaximumAttempts = 1,
            },
            logger
        );

        await subject.Execute(TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Mongo migration attempt 1 failed.");
        logger
            .Messages.Should()
            .Contain(
                "Mongo migrations did not complete after 1 attempt(s). No further attempts will be made by this host."
            );
        logger.Messages.Should().Contain("Mongo migration lease could not be released. It will expire automatically.");
    }

    private static TestableMongoMigrationService CreateSubject(
        IMongoMigrationLeaseService leaseService,
        IMongoMigrationRunner migrationRunner,
        MongoMigrationOptions? options = null,
        ILogger<MongoMigrationService>? logger = null,
        TimeProvider? timeProvider = null
    ) =>
        new(
            leaseService,
            migrationRunner,
            Options.Create(
                options
                    ?? new MongoMigrationOptions
                    {
                        LeaseDurationSeconds = 2,
                        LeaseRenewalIntervalSeconds = 1,
                        AttemptTimeoutSeconds = 1,
                        RetryDelaySeconds = 1,
                        MaximumAttempts = 2,
                    }
            ),
            timeProvider ?? TimeProvider.System,
            logger ?? Substitute.For<ILogger<MongoMigrationService>>()
        );

    private sealed class TestableMongoMigrationService(
        IMongoMigrationLeaseService leaseService,
        IMongoMigrationRunner migrationRunner,
        IOptions<MongoMigrationOptions> options,
        TimeProvider timeProvider,
        ILogger<MongoMigrationService> logger
    ) : MongoMigrationService(leaseService, migrationRunner, options, timeProvider, logger)
    {
        public Task Execute(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }
}
