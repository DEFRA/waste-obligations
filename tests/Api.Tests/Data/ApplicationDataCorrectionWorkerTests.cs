using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Data;

public class ApplicationDataCorrectionWorkerTests
{
    [Fact]
    public async Task Execute_ShouldWaitForMigrationsAndRenewItsOwnLeaseBeyondFiveMinutes()
    {
        var migrations = new MongoMigrationCompletion();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease
            .TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                renewed.TrySetResult();
                return true;
            });
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(async x =>
            {
                started.TrySetResult();
                await finish.Task.WaitAsync(x.Arg<CancellationToken>());
            });
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var time = new FakeTimeProvider();
        using var subject = CreateSubject(migrations, services, lease, time);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await lease.DidNotReceive().TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await runner.DidNotReceive().Run(Arg.Any<CancellationToken>());

        migrations.Complete();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(301));
        await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        subject.ExecuteTask!.IsCompleted.Should().BeFalse();
        await lease.DidNotReceive().Release(Arg.Any<CancellationToken>());
        finish.SetResult();
        await subject.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        await runner.Received(1).Run(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_WhenLeaseIsLost_ShouldCancelWorkBeforeReleasingLease(bool throwingCallback)
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        lease.TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease
            .Release(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stopped.Task.IsCompleted.Should().BeTrue();
                released.TrySetResult();

                return Task.CompletedTask;
            });
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(async x =>
            {
                var token = x.Arg<CancellationToken>();
                if (throwingCallback)
                    token.Register(() =>
                        throw new InvalidOperationException("Correction cancellation callback failed")
                    );
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
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var time = new FakeTimeProvider();
        using var subject = CreateSubject(migrations, services, lease, time);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(15));
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);
        await runner.Received(1).Run(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenRenewalHangs_ShouldCancelOwnedWorkAndDrainBeforeReleasing()
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
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
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease
            .Release(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                released.TrySetResult();
                return Task.CompletedTask;
            });
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
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
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var time = new FakeTimeProvider();
        using var subject = CreateSubject(migrations, services, lease, time);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(15));
        await renewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await lease.DidNotReceive().Release(Arg.Any<CancellationToken>());
        subject.ExecuteTask!.IsCompleted.Should().BeFalse();

        renewal.SetResult(false);
        await released.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await subject.StopAsync(TestContext.Current.CancellationToken);
        await runner.Received(1).Run(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenAcquisitionHangsThenSucceedsLate_ShouldNotStartCorrectionWork()
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        var acquisitionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().Register(() => cancelled.TrySetResult());
                acquisitionStarted.TrySetResult();

                return acquired.Task;
            });
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var time = new FakeTimeProvider();
        using var subject = CreateSubject(migrations, services, lease, time);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await acquisitionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(10));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        acquired.SetResult(true);
        await subject.StopAsync(TestContext.Current.CancellationToken);

        await runner.DidNotReceive().Run(Arg.Any<CancellationToken>());
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenRunnerFailsDuringShutdown_ShouldReleaseAndLogTheOriginalError()
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().Register(() => cancelled.TrySetResult());
                started.TrySetResult();

                return work.Task;
            });
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(migrations, services, lease, TimeProvider.System, logger);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stopping = subject.StopAsync(TestContext.Current.CancellationToken);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var failure = new InvalidOperationException("Correction failed while stopping");
        work.SetException(failure);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        logger.Entries.Should().Contain(x => x.Level == LogLevel.Error && x.Exception == failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_WhenReleaseFailsAfterSuccessfulWork_ShouldWarnWithoutRerunning(bool timesOut)
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var releasing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new InvalidOperationException("Release failed");
        lease
            .Release(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                releasing.TrySetResult();

                return timesOut
                    ? Task.Delay(Timeout.InfiniteTimeSpan, x.Arg<CancellationToken>())
                    : Task.FromException(failure);
            });
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var time = new FakeTimeProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(migrations, services, lease, time, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await releasing.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (timesOut)
            time.Advance(TimeSpan.FromSeconds(10));
        await subject.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await runner.Received(1).Run(Arg.Any<CancellationToken>());
        await lease.Received(1).TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
        if (timesOut)
            logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning && x.Exception is TimeoutException);
        else
            logger.Entries.Should().Contain(x => x.Level == LogLevel.Warning && x.Exception == failure);
    }

    [Fact]
    public async Task Execute_WhenCleanupCallbackFailsAfterSuccessfulWork_ShouldReleaseAndReportWithoutRerunning()
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var failure = new InvalidOperationException("Correction cleanup callback failed");
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().Register(() => throw failure);

                return Task.CompletedTask;
            });
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(migrations, services, lease, TimeProvider.System, logger);

        await subject.StartAsync(TestContext.Current.CancellationToken);
        await subject.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await runner.Received(1).Run(Arg.Any<CancellationToken>());
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        logger
            .Entries.Single(x => x.Level == LogLevel.Error)
            .Exception.Should()
            .BeOfType<AggregateException>()
            .Which.Flatten()
            .InnerExceptions.Should()
            .ContainSingle()
            .Which.Should()
            .BeSameAs(failure);
    }

    [Fact]
    public async Task Execute_WhenHostStopsDuringWork_ShouldDrainAndReleaseWithoutAnError()
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasedAfterStop = false;
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
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
        lease
            .Release(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                releasedAfterStop = stopped.Task.IsCompleted;

                return Task.CompletedTask;
            });
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(migrations, services, lease, TimeProvider.System, logger);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await subject
            .StopAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        releasedAfterStop.Should().BeTrue();
        logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
        subject.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Execute_WhenAttemptsFail_ShouldStopAtLimit(bool acquisitionFails)
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var failure = new InvalidOperationException("Permanent failure");
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease
            .TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => acquisitionFails ? Task.FromException<bool>(failure) : Task.FromResult(true));
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        runner.Run(Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(
            migrations,
            services,
            lease,
            TimeProvider.System,
            logger,
            new ApplicationDataCorrectionOptions { MaximumAttempts = 2, RetryDelaySeconds = 1 }
        );
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await subject.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await lease.Received(2).TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await runner.Received(acquisitionFails ? 0 : 2).Run(Arg.Any<CancellationToken>());
        logger.Messages.Should().ContainSingle(x => x.Contains("No further attempts"));
    }

    [Fact]
    public async Task Execute_WhenLeaseIsContended_ShouldPreserveAttemptForSuccessfulRun()
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        using var subject = CreateSubject(
            migrations,
            services,
            lease,
            TimeProvider.System,
            options: new ApplicationDataCorrectionOptions { MaximumAttempts = 1, RetryDelaySeconds = 1 }
        );
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await subject.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await runner.Received(1).Run(Arg.Any<CancellationToken>());
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WhenMigrationsFail_ShouldLogBlockedAndNeverAcquireLease()
    {
        var migrations = new MongoMigrationCompletion();
        var failure = new InvalidOperationException("Migrations exhausted attempts");
        migrations.Fail(failure);
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        using var services = new ServiceCollection().BuildServiceProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(migrations, services, lease, TimeProvider.System, logger);
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await subject.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await lease.DidNotReceive().TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        logger.Entries.Should().ContainSingle(x => x.Level == LogLevel.Warning && x.Exception == failure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Execute_WhenLeaseCancelsWork_ShouldKeepSuccessfulReturnAndAvoidDuplicateError(
        bool finishesSuccessfully
    )
    {
        var migrations = new MongoMigrationCompletion();
        migrations.Complete();
        var lease = Substitute.For<IApplicationDataCorrectionLeaseService>();
        lease.TryAcquire(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        lease.TryRenew(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = Substitute.For<IApplicationDataCorrectionRunner>();
        runner
            .Run(Arg.Any<CancellationToken>())
            .Returns(async x =>
            {
                started.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, x.Arg<CancellationToken>());
                }
                catch (OperationCanceledException) when (finishesSuccessfully)
                {
                    // Simulate a runner that acknowledges success as cancellation arrives.
                }
            });
        using var services = new ServiceCollection().AddSingleton(runner).BuildServiceProvider();
        var time = new FakeTimeProvider();
        var logger = new RecordingLogger<ApplicationDataCorrectionWorker>();
        using var subject = CreateSubject(
            migrations,
            services,
            lease,
            time,
            logger,
            new ApplicationDataCorrectionOptions { MaximumAttempts = 1 }
        );
        await subject.StartAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(15));
        await subject.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await runner.Received(1).Run(Arg.Any<CancellationToken>());
        await lease.Received(1).Release(Arg.Any<CancellationToken>());
        logger.Messages.Should().NotContain("Application data corrections failed.");
        logger.Entries.Count(x => x.Level == LogLevel.Error).Should().Be(finishesSuccessfully ? 1 : 2);
    }

    private static ApplicationDataCorrectionWorker CreateSubject(
        MongoMigrationCompletion migrations,
        IServiceProvider services,
        IApplicationDataCorrectionLeaseService lease,
        TimeProvider time,
        ILogger<ApplicationDataCorrectionWorker>? logger = null,
        ApplicationDataCorrectionOptions? options = null
    ) =>
        new(
            migrations,
            services.GetRequiredService<IServiceScopeFactory>(),
            lease,
            Options.Create(options ?? new ApplicationDataCorrectionOptions()),
            time,
            logger ?? Substitute.For<ILogger<ApplicationDataCorrectionWorker>>()
        );
}
