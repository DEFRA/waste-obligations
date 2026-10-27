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
    public async Task Execute_WhenLeaseRenewalFails_ShouldCancelMigrationAndRespectAttemptLimit()
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
        var subject = CreateSubject(
            leaseService,
            migrationRunner,
            new MongoMigrationOptions { LeaseRenewalIntervalSeconds = 1, MaximumAttempts = 1 },
            logger
        );

        await subject.Execute(TestContext.Current.CancellationToken);
        await migrationCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Mongo migration lease was not renewed. Cancelling the migration engine.");
        await migrationRunner.Received(1).Run(Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenLeaseRenewalThrowsAnUnexpectedCancellation_ShouldCancelMigrationAndRespectAttemptLimit()
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
        var subject = CreateSubject(
            leaseService,
            migrationRunner,
            new MongoMigrationOptions { LeaseRenewalIntervalSeconds = 1, MaximumAttempts = 1 },
            logger
        );

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

        logger.Messages.Should().Contain("Mongo migration lease acquisition failed while the host was stopping.");
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
    public async Task Execute_WhenLeaseBecomesAvailableAfterRetry_ShouldRunMigrations()
    {
        var timeProvider = new RetryAwareTimeProvider(TimeSpan.FromSeconds(5));
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var migrationRunner = Substitute.For<IMongoMigrationRunner>();
        var subject = CreateSubject(leaseService, migrationRunner, timeProvider: timeProvider);
        var execution = subject.Execute(TestContext.Current.CancellationToken);

        await timeProvider.RetryScheduled.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await leaseService.Received(1).TryAcquire(TimeSpan.FromSeconds(2), Arg.Any<CancellationToken>());
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await leaseService.Received(2).TryAcquire(TimeSpan.FromSeconds(2), Arg.Any<CancellationToken>());
        await migrationRunner.Received(1).Run(Arg.Any<CancellationToken>());
        await leaseService.Received(1).Release(Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task Execute_WhenRenewalHangs_ShouldCancelMigrationsAndDrainBeforeReleasing()
    {
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var renewalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lease
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                renewalStarted.TrySetResult();

                return renewal.Task;
            });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = Substitute.For<IMongoMigrationRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(async x =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, x.Arg<CancellationToken>());
                }
                finally
                {
                    stopped.TrySetResult();
                }
            });
        var time = new FakeTimeProvider();
        var subject = CreateSubject(
            lease,
            runner,
            new MongoMigrationOptions { MaximumAttempts = 1 },
            timeProvider: time
        );
        var execution = subject.Execute(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(15));
        await renewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await lease.DidNotReceive().Release(Arg.Any<CancellationToken>());
        execution.IsCompleted.Should().BeFalse();

        renewal.SetResult(false);
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await runner.Received(1).Run(Arg.Any<CancellationToken>());
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Execute_WhenRenewalSucceedsLate_ShouldReacquireAndKeepTheAttemptBudget(bool secondSucceeds)
    {
        var time = new RetryAwareTimeProvider(TimeSpan.FromSeconds(1));
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        var order = new List<string>();
        lease
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("acquire");

                return true;
            });
        lease
            .Release(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("release");

                return Task.CompletedTask;
            });
        var renewalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lease
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                renewalStarted.TrySetResult();

                return renewal.Task;
            });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new List<CancellationToken>();
        var secondStartedCancelled = true;
        var runner = Substitute.For<IMongoMigrationRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                order.Add("run");
                tokens.Add(x.Arg<CancellationToken>());
                if (tokens.Count == 1)
                {
                    tokens[0].Register(() => cancelled.TrySetResult());
                    started.TrySetResult();

                    return first.Task;
                }
                secondStartedCancelled = x.Arg<CancellationToken>().IsCancellationRequested;

                return secondSucceeds
                    ? Task.CompletedTask
                    : Task.FromException(new InvalidOperationException("Second attempt failed"));
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(
            lease,
            runner,
            new MongoMigrationOptions { MaximumAttempts = 2, RetryDelaySeconds = 1 },
            logger,
            time
        );
        var execution = subject.Execute(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(15));
        await renewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await lease.DidNotReceive().Release(Arg.Any<CancellationToken>());
        order.Should().Equal("acquire", "run");

        first.SetCanceled(tokens[0]);
        await lease.DidNotReceive().Release(Arg.Any<CancellationToken>());
        renewal.SetResult(true);
        await time.RetryScheduled.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(1));
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        order.Should().Equal("acquire", "run", "release", "acquire", "run", "release");
        secondStartedCancelled.Should().BeFalse();
        tokens.Should().HaveCount(2);
        if (!secondSucceeds)
            logger
                .Messages.Should()
                .Contain(
                    "Mongo migrations did not complete after 2 attempt(s). No further attempts will be made by this host."
                );
    }

    [Fact]
    public async Task Execute_WhenAcquisitionSucceedsLateDuringShutdown_ShouldReleaseWithoutStartingMigrations()
    {
        var time = new FakeTimeProvider();
        using var stopping = new CancellationTokenSource();
        var acquisition = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        lease
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().Register(() => cancelled.TrySetResult());

                return acquisition.Task;
            });
        var runner = Substitute.For<IMongoMigrationRunner>();
        var subject = CreateSubject(
            lease,
            runner,
            new MongoMigrationOptions { LeaseOperationTimeoutSeconds = 1 },
            timeProvider: time
        );
        var execution = subject.Execute(stopping.Token);
        time.Advance(TimeSpan.FromSeconds(1));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await stopping.CancelAsync();
        await lease.DidNotReceive().Release(Arg.Any<CancellationToken>());
        acquisition.SetResult(true);
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        await runner.DidNotReceive().Run(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenEngineFailsAfterTimeout_ShouldLogTheActualFailure()
    {
        var time = new FakeTimeProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var runner = Substitute.For<IMongoMigrationRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().Register(() => cancelled.TrySetResult());
                started.TrySetResult();

                return engine.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(
            lease,
            runner,
            new MongoMigrationOptions { AttemptTimeoutSeconds = 1, MaximumAttempts = 1 },
            logger,
            time
        );
        var execution = subject.Execute(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(1));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var failure = new InvalidOperationException("Engine failed while draining");
        engine.SetException(failure);
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger
            .Entries.Single(x => x.Message == "Mongo migration attempt 1 failed.")
            .Exception.Should()
            .BeSameAs(failure);
    }

    [Fact]
    public async Task Execute_WhenShutdownIsStillDraining_ShouldWarnWithoutRaisingTimeoutErrors()
    {
        var time = new FakeTimeProvider();
        using var stopping = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var runner = Substitute.For<IMongoMigrationRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                started.TrySetResult();

                return engine.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(
            lease,
            runner,
            new MongoMigrationOptions { AttemptTimeoutSeconds = 1 },
            logger,
            time
        );
        var execution = subject.Execute(stopping.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await stopping.CancelAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await AsyncWaiter.WaitForAsync(
            () =>
            {
                logger.Messages.Should().Contain(x => x.Contains("cancellation is still draining"));

                return Task.CompletedTask;
            },
            5
        );
        execution.IsCompleted.Should().BeFalse();
        engine.SetResult();
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
        logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Execute_WhenReleaseTimesOut_ShouldLogAReleaseTimeout()
    {
        var time = new FakeTimeProvider();
        var releaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        lease
            .Release(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                releaseStarted.TrySetResult();

                return Task.Delay(Timeout.InfiniteTimeSpan, x.Arg<CancellationToken>());
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(lease, Substitute.For<IMongoMigrationRunner>(), logger: logger, timeProvider: time);
        var execution = subject.Execute(TestContext.Current.CancellationToken);
        await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        logger.Messages.Should().Contain("Mongo migration lease release timed out. It will expire automatically.");
    }

    [Fact]
    public async Task Execute_WhenRenewalAndCancellationCallbackFail_ShouldStillDrainAndRelease()
    {
        var time = new FakeTimeProvider();
        var lease = Substitute.For<IMongoMigrationLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        lease
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("Renewal failed")));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = Substitute.For<IMongoMigrationRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(async x =>
            {
                var token = x.Arg<CancellationToken>();
                token.Register(() => throw new InvalidOperationException("Migration cancellation callback failed"));
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        var subject = CreateSubject(lease, runner, new MongoMigrationOptions { MaximumAttempts = 1 }, logger, time);
        var execution = subject.Execute(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(15));
        await execution.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        logger
            .Entries.Should()
            .Contain(x =>
                x.Exception != null
                && x.Exception.ToString().Contains("Migration cancellation callback failed", StringComparison.Ordinal)
            );
    }

    private sealed class RetryAwareTimeProvider(TimeSpan retryDelay) : TimeProvider
    {
        private readonly FakeTimeProvider _clock = new();
        private readonly TaskCompletionSource _retry = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RetryScheduled => _retry.Task;
        public override long TimestampFrequency => _clock.TimestampFrequency;

        public override long GetTimestamp() => _clock.GetTimestamp();

        public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();

        public void Advance(TimeSpan delay) => _clock.Advance(delay);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = _clock.CreateTimer(callback, state, dueTime, period);
            if (dueTime == retryDelay && period == Timeout.InfiniteTimeSpan)
                _retry.TrySetResult();

            return timer;
        }
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
