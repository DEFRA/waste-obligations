using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.ObligationCalculations;

public class ObligationCalculationInstructionProcessorTests : IntegrationTestBase
{
    [Fact]
    public async Task ProcessNext_ShouldCompleteOldestPendingInstructionAndLogCompletion()
    {
        var newer = ObligationCalculationInstructionFixture.Default().With(x => x.SubmitterId, Guid.NewGuid()).Create();
        var older = ObligationCalculationInstructionFixture
            .Default()
            .With(x => x.CreatedAt, newer.CreatedAt.AddMinutes(-1))
            .Create();
        var terminal = ObligationCalculationInstructionFixture
            .Default()
            .With(x => x.Status, ObligationCalculationInstructionStatus.Completed)
            .With(x => x.CreatedAt, older.CreatedAt.AddMinutes(-1))
            .Create();
        await ObligationCalculationInstructions.InsertManyAsync(
            [newer, terminal, older],
            cancellationToken: TestContext.Current.CancellationToken
        );
        var logger = new RecordingLogger<ObligationCalculationInstructionProcessor>();
        var processor = new ObligationCalculationInstructionProcessor(GetMongoApplicationDatabase(), logger);

        var processed = await processor.ProcessNext(TestContext.Current.CancellationToken);

        processed.Should().BeTrue();
        (await Read(older))
            .Should()
            .BeEquivalentTo(older with { Status = ObligationCalculationInstructionStatus.Completed });
        (await Read(newer)).Should().BeEquivalentTo(newer);
        (await Read(terminal)).Should().BeEquivalentTo(terminal);
        logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Information
                && x.Message.Contains("completed")
                && x.Message.Contains("no calculation performed")
            );
    }

    [Theory]
    [InlineData(ObligationCalculationInstructionStatus.Running)]
    [InlineData(ObligationCalculationInstructionStatus.Completed)]
    [InlineData(ObligationCalculationInstructionStatus.Failed)]
    public async Task ProcessNext_WhenNoPendingInstructionExists_ShouldLeaveOtherStatesUnchanged(
        ObligationCalculationInstructionStatus status
    )
    {
        var instruction = ObligationCalculationInstructionFixture.Default().With(x => x.Status, status).Create();
        await ObligationCalculationInstructions.InsertOneAsync(
            instruction,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var logger = new RecordingLogger<ObligationCalculationInstructionProcessor>();
        var processor = new ObligationCalculationInstructionProcessor(GetMongoApplicationDatabase(), logger);

        var processed = await processor.ProcessNext(TestContext.Current.CancellationToken);

        processed.Should().BeFalse();
        (await Read(instruction)).Should().BeEquivalentTo(instruction);
        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ProcessNext_WhenHostsCompete_ShouldCompleteEachInstructionOnlyOnce(int instructionCount)
    {
        var instructions = Enumerable
            .Range(0, instructionCount)
            .Select(_ =>
                ObligationCalculationInstructionFixture.Default().With(x => x.SubmitterId, Guid.NewGuid()).Create()
            )
            .ToArray();
        await ObligationCalculationInstructions.InsertManyAsync(
            instructions,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var processors = Enumerable
            .Range(0, 3)
            .Select(_ => new ObligationCalculationInstructionProcessor(
                GetMongoApplicationDatabase(),
                new RecordingLogger<ObligationCalculationInstructionProcessor>()
            ))
            .ToArray();

        var results = await Task.WhenAll(processors.Select(x => x.ProcessNext(TestContext.Current.CancellationToken)));

        results.Count(x => x).Should().Be(instructionCount);
        foreach (var instruction in instructions)
            (await Read(instruction)).Status.Should().Be(ObligationCalculationInstructionStatus.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessNext_ShouldAllowFreshInstructionForCompletedScope(bool submitterSpecific)
    {
        Guid? submitterId = submitterSpecific ? Guid.NewGuid() : null;
        var creation = new ObligationCalculationInstructionService(
            GetMongoApplicationDatabase(),
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.Zero))
        );
        var first = await creation.Create(2026, submitterId, TestContext.Current.CancellationToken);
        var processor = new ObligationCalculationInstructionProcessor(
            GetMongoApplicationDatabase(),
            new RecordingLogger<ObligationCalculationInstructionProcessor>()
        );

        await processor.ProcessNext(TestContext.Current.CancellationToken);
        var next = await creation.Create(2026, submitterId, TestContext.Current.CancellationToken);

        next.Id.Should().NotBe(first.Id);
        next.Status.Should().Be(ObligationCalculationInstructionStatus.Pending);
        (await Read(first)).Status.Should().Be(ObligationCalculationInstructionStatus.Completed);
    }

    [Fact]
    public async Task Worker_ShouldCompleteInstructionThroughRegisteredProcessor()
    {
        var instruction = ObligationCalculationInstructionFixture.Default().Create();
        await ObligationCalculationInstructions.InsertOneAsync(
            instruction,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ObligationCalculationProcessing:Enabled"] = "true",
                    ["ObligationCalculationProcessing:PollIntervalSeconds"] = "1",
                }
            )
            .Build();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(GetMongoApplicationDatabase())
            .AddObligationCalculationProcessing()
            .BuildServiceProvider();
        var worker = services.GetRequiredService<IHostedService>();

        try
        {
            await worker.StartAsync(TestContext.Current.CancellationToken);
            await WaitForAsync(async () =>
                (await Read(instruction)).Status.Should().Be(ObligationCalculationInstructionStatus.Completed)
            );
        }
        finally
        {
            await worker.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private Task<ObligationCalculationInstruction> Read(ObligationCalculationInstruction instruction) =>
        ObligationCalculationInstructions
            .Find(x => x.Id == instruction.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
}
