using System.Threading.Channels;
using AwesomeAssertions;
using Cronos;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.Scheduling;

public class ScheduledJobTests : IntegrationTestBase
{
    private const string JobName = "test-scheduled-job";
    private const string LeaseCollection = "_scheduled_job_leases";
    private const string StateCollection = "_scheduled_job_state";
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 0, 59, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(2027, 1, 31, 2026)]
    [InlineData(2027, 2, 1, 2027)]
    public async Task ConfiguredJob_ShouldCreateInstructionForCurrentObligationYear(
        int year,
        int month,
        int day,
        int expectedYear
    )
    {
        await Reset();
        _clock.SetUtcNow(new DateTimeOffset(year, month, day, 0, 59, 0, TimeSpan.Zero));
        var time = new SchedulerTimeProvider(_clock);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ObligationCalculation:Enabled"] = "true",
                    ["ObligationCalculation:Cron"] = "0 1 * * *",
                }
            )
            .Build();
        await using var host = new ServiceCollection()
            .AddLogging()
            .AddSingleton(GetMongoApplicationDatabase())
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<ICurrentObligationYearProvider, CurrentObligationYearProvider>()
            .AddTransient<IObligationCalculationInstructionService, ObligationCalculationInstructionService>()
            .AddScheduledJobs(configuration)
            .BuildServiceProvider();
        var worker = host.GetRequiredService<IHostedService>();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await time.WaitForTimer(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await time.WaitForTimer(TimeSpan.FromDays(1));

        var instruction = await ObligationCalculationInstructions
            .Find(x => x.ObligationYear == expectedYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        instruction.SubmitterId.Should().BeNull();
        instruction.Status.Should().Be(ObligationCalculationInstructionStatus.Pending);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Shutdown_ShouldKeepRenewingLeaseUntilCancelledJobFinishes()
    {
        await Reset();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var control = new JobControl
        {
            Run = async token =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                await finish.Task;
            },
        };
        var time = new SchedulerTimeProvider(_clock);
        await using var host = CreateHost(time, control, "* * * * *");
        var worker = host.GetRequiredService<IHostedService>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await time.WaitForTimer(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForAsync(() =>
        {
            control.Count.Should().Be(1);
            return Task.CompletedTask;
        });
        var initialExpiry = (await ReadLease())["expiresAt"].ToUniversalTime();

        var stopping = worker.StopAsync(TestContext.Current.CancellationToken);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(40));
        await WaitForAsync(async () =>
            (await ReadLease())["expiresAt"].ToUniversalTime().Should().BeAfter(initialExpiry)
        );

        stopping.IsCompleted.Should().BeFalse();
        control.Count.Should().Be(1);
        finish.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        (await ReadLease()).Contains("owner").Should().BeFalse();
    }

    [Fact]
    public async Task Schedule_ShouldRunOnceAcrossHostsIncludingAHostCheckingAfterCompletion()
    {
        await Reset();
        var control = new JobControl();
        var firstTime = new SchedulerTimeProvider(_clock);
        var secondTime = new SchedulerTimeProvider(_clock);
        await using var first = CreateHost(firstTime, control);
        await using var second = CreateHost(secondTime, control);
        var firstWorker = first.GetRequiredService<IHostedService>();
        var secondWorker = second.GetRequiredService<IHostedService>();
        await firstWorker.StartAsync(TestContext.Current.CancellationToken);
        await secondWorker.StartAsync(TestContext.Current.CancellationToken);
        await firstTime.WaitForTimer(TimeSpan.FromMinutes(1));
        await secondTime.WaitForTimer(TimeSpan.FromMinutes(1));
        control.Count.Should().Be(0);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await firstTime.WaitForTimer(TimeSpan.FromDays(1));
        await secondTime.WaitForTimer(TimeSpan.FromDays(1));

        control.Count.Should().Be(1);
        var state = await GetMongoDatabase()
            .GetCollection<BsonDocument>(StateCollection)
            .Find(new BsonDocument("_id", JobName))
            .SingleAsync(TestContext.Current.CancellationToken);
        state["lastStartedAt"].ToUniversalTime().Should().Be(_clock.GetUtcNow().UtcDateTime);

        var lateClock = new FakeTimeProvider(_clock.GetUtcNow().AddMinutes(-1));
        var lateTime = new SchedulerTimeProvider(lateClock);
        await using var late = CreateHost(lateTime, control);
        var lateWorker = late.GetRequiredService<IHostedService>();
        await lateWorker.StartAsync(TestContext.Current.CancellationToken);
        await lateTime.WaitForTimer(TimeSpan.FromMinutes(1));
        lateClock.Advance(TimeSpan.FromMinutes(1));
        await lateTime.WaitForTimer(TimeSpan.FromDays(1));

        control.Count.Should().Be(1);
        await lateWorker.StopAsync(TestContext.Current.CancellationToken);
        await firstWorker.StopAsync(TestContext.Current.CancellationToken);
        await secondWorker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LongRunningJob_ShouldRenewLeaseAndExcludeOtherHostsUntilFinished()
    {
        await Reset();
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var control = new JobControl { Run = _ => finish.Task };
        var time = new SchedulerTimeProvider(_clock);
        await using var host = CreateHost(time, control, "* * * * *");
        var worker = host.GetRequiredService<IHostedService>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await time.WaitForTimer(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForAsync(() =>
        {
            control.Count.Should().Be(1);
            return Task.CompletedTask;
        });
        var lease = await ReadLease();
        var initialExpiry = lease["expiresAt"].ToUniversalTime();

        _clock.Advance(TimeSpan.FromSeconds(40));
        await WaitForAsync(async () =>
            (await ReadLease())["expiresAt"].ToUniversalTime().Should().BeAfter(initialExpiry)
        );

        var secondTime = new SchedulerTimeProvider(_clock);
        await using var second = CreateHost(secondTime, control, "* * * * *");
        var secondWorker = second.GetRequiredService<IHostedService>();
        await secondWorker.StartAsync(TestContext.Current.CancellationToken);
        await secondTime.WaitForTimer(TimeSpan.FromSeconds(20));
        _clock.Advance(TimeSpan.FromSeconds(20));
        await secondTime.WaitForTimer(TimeSpan.FromMinutes(1));

        control.Count.Should().Be(1);
        finish.SetResult();
        await time.WaitForTimer(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForAsync(() =>
        {
            control.Count.Should().Be(2);
            return Task.CompletedTask;
        });

        await worker.StopAsync(TestContext.Current.CancellationToken);
        await secondWorker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FailedJob_ShouldReleaseLeaseAndRunAtNextOccurrence()
    {
        await Reset();
        var control = new JobControl { Run = _ => Task.FromException(new InvalidOperationException("Job failed")) };
        var time = new SchedulerTimeProvider(_clock);
        await using var host = CreateHost(time, control, "* * * * *");
        var worker = host.GetRequiredService<IHostedService>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await time.WaitForTimer(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await time.WaitForTimer(TimeSpan.FromMinutes(1));

        control.Count.Should().Be(1);
        (await ReadLease()).Contains("owner").Should().BeFalse();

        _clock.Advance(TimeSpan.FromMinutes(1));
        await time.WaitForTimer(TimeSpan.FromMinutes(1));

        control.Count.Should().Be(2);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LostLease_ShouldCancelAndDrainJobBeforeStartingAnotherOccurrence()
    {
        await Reset();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var control = new JobControl
        {
            Run = async token =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                await finish.Task;
            },
        };
        var time = new SchedulerTimeProvider(_clock);
        await using var host = CreateHost(time, control, "* * * * *");
        var worker = host.GetRequiredService<IHostedService>();
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await time.WaitForTimer(TimeSpan.FromMinutes(1));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForAsync(() =>
        {
            control.Count.Should().Be(1);
            return Task.CompletedTask;
        });
        await GetMongoDatabase()
            .GetCollection<BsonDocument>(LeaseCollection)
            .UpdateOneAsync(
                new BsonDocument("_id", JobName),
                new BsonDocument("$set", new BsonDocument("owner", "another-host")),
                cancellationToken: TestContext.Current.CancellationToken
            );

        _clock.Advance(TimeSpan.FromSeconds(20));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(2));

        control.Count.Should().Be(1);
        (await ReadLease())["owner"].AsString.Should().Be("another-host");
        finish.SetResult();
        await time.WaitForTimer(TimeSpan.FromSeconds(40));
        (await ReadLease())["owner"].AsString.Should().Be("another-host");
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    private static ServiceProvider CreateHost(
        SchedulerTimeProvider time,
        JobControl control,
        string cron = "0 1 * * *"
    ) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton(GetMongoApplicationDatabase())
            .AddSingleton<TimeProvider>(time)
            .AddSingleton(control)
            .AddScoped<TestJob>()
            .AddSingleton<IHostedService>(provider => new ScheduledJobWorker(
                JobName,
                CronExpression.Parse(cron),
                typeof(TestJob),
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<IMongoDatabase>(),
                time,
                NullLogger<ScheduledJobWorker>.Instance
            ))
            .BuildServiceProvider();

    private static async Task Reset()
    {
        await GetMongoDatabase().DropCollectionAsync(LeaseCollection, TestContext.Current.CancellationToken);
        await GetMongoDatabase().DropCollectionAsync(StateCollection, TestContext.Current.CancellationToken);
    }

    private static Task<BsonDocument> ReadLease() =>
        GetMongoDatabase()
            .GetCollection<BsonDocument>(LeaseCollection)
            .Find(new BsonDocument("_id", JobName))
            .SingleAsync(TestContext.Current.CancellationToken);

    private sealed class JobControl
    {
        public int Count;
        public Func<CancellationToken, Task> Run { get; init; } = _ => Task.CompletedTask;
    }

    private sealed class TestJob(JobControl control) : IScheduledJob
    {
        public Task Run(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref control.Count);

            return control.Run(cancellationToken);
        }
    }

    private sealed class SchedulerTimeProvider(FakeTimeProvider clock) : TimeProvider
    {
        private readonly Channel<TimeSpan> _timers = Channel.CreateUnbounded<TimeSpan>();

        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = clock.CreateTimer(callback, state, dueTime, period);
            _timers.Writer.TryWrite(dueTime);

            return timer;
        }

        public async Task WaitForTimer(TimeSpan dueTime)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (await _timers.Reader.ReadAsync(timeout.Token) != dueTime)
            {
                // Ignore unrelated lease-renewal timers.
            }
        }
    }
}
