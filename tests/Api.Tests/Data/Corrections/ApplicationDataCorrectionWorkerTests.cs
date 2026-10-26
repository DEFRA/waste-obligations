using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Corrections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Data.Corrections;

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

    [Fact]
    public async Task Execute_WhenLeaseIsLost_ShouldCancelWorkBeforeReleasingLease()
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

    private static ApplicationDataCorrectionWorker CreateSubject(
        MongoMigrationCompletion migrations,
        IServiceProvider services,
        IApplicationDataCorrectionLeaseService lease,
        TimeProvider time
    ) =>
        new(
            migrations,
            services.GetRequiredService<IServiceScopeFactory>(),
            lease,
            Options.Create(new ApplicationDataCorrectionOptions()),
            time,
            Substitute.For<ILogger<ApplicationDataCorrectionWorker>>()
        );
}
