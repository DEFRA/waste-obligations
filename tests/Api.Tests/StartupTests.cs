using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests;

public class StartupTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public void WhenFaultOnStartup_ShouldThrow()
    {
        var builder = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services => services.AddHostedService<FaultyStartupService>());
        });

        var act = () => builder.CreateClient();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task WhenMongoMigrationDoesNotComplete_ShouldStartAndServeHealth()
    {
        var leaseService = Substitute.For<IMongoMigrationLeaseService>();
        leaseService.InstanceId.Returns("test-instance");
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
        var migrationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                migrationStarted.TrySetResult();

                return migrationCompletion.Task;
            });
        var logger = new RecordingLogger<MongoMigrationService>();
        using var application = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHostedService(_ => new MongoMigrationService(
                    leaseService,
                    migrationRunner,
                    Options.Create(
                        new MongoMigrationOptions
                        {
                            LeaseDurationSeconds = 10,
                            LeaseRenewalIntervalSeconds = 1,
                            AttemptTimeoutSeconds = 1,
                            RetryDelaySeconds = 60,
                            MaximumAttempts = 2,
                        }
                    ),
                    logger
                ));
            });
        });
        var clientTask = Task.Run(application.CreateClient);

        try
        {
            using var client = await clientTask.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            await migrationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            application.Services.GetServices<IHostedService>().OfType<MongoMigrationService>().Should().ContainSingle();

            var initialHealthResponse = await client.GetAsync("/health", TestContext.Current.CancellationToken);

            initialHealthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            await migrationCancellationRequested.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            await leaseRenewed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var timedOutMigrationHealthResponse = await client.GetAsync(
                "/health",
                TestContext.Current.CancellationToken
            );

            timedOutMigrationHealthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            logger.Messages.Should().Contain(message => message.Contains("exclusive lease will be retained"));
            await migrationRunner.Received(1).Run(Arg.Any<CancellationToken>());
        }
        finally
        {
            migrationCompletion.TrySetResult();

            if (!clientTask.IsCompleted)
                await clientTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    public class FaultyStartupService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated startup crash");

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
