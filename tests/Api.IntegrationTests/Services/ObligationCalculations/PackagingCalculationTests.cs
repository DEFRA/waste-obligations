using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.ScheduledJobs;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Defra.WasteObligations.Testing.Fixtures.WastePackaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.ObligationCalculations;

public class PackagingCalculationTests : IntegrationTestBase
{
    private static readonly Guid SubmitterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly RecordingLogger<ObligationCalculationProcessor> _logger = new();

    [Fact]
    public async Task Process_MultipleSubmittersWithMultipleProducers_SavesOnlyTheirOwnResults()
    {
        var schemeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var subsidiaryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var member1 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var member2 = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var memberSubsidiary = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var direct = new ApprovedSubmission
        {
            SubmitterOrganisationId = SubmitterId,
            SubmitterType = ObligationSubmitterType.DirectRegistrant,
            Producers = [Producer(SubmitterId, 40_000, 40_000), Producer(subsidiaryId, 70_000, 70_000)],
        };
        var scheme = new ApprovedSubmission
        {
            SubmitterOrganisationId = schemeId,
            SubmitterType = ObligationSubmitterType.ComplianceScheme,
            Producers = [Producer(member1, 21_000), Producer(member2, 21_000), Producer(memberSubsidiary, 21_000)],
        };
        var source = Source(direct, scheme);
        var instruction = ObligationCalculationInstructionFixture.Default().With(x => x.ObligationYear, 2025).Create();

        await Processor(source).Process(instruction, TestContext.Current.CancellationToken);

        await source.Received(1).Read(2024, TestContext.Current.CancellationToken);
        var savedDirect = await Read(SubmitterId, 2025);
        var savedScheme = await Read(schemeId, 2025);
        savedDirect.SubmitterType.Should().Be(ObligationSubmitterType.DirectRegistrant);
        savedScheme.SubmitterType.Should().Be(ObligationSubmitterType.ComplianceScheme);
        savedDirect
            .Producers.Select(x => x.ProducerOrganisationId)
            .Should()
            .BeEquivalentTo(new[] { SubmitterId, subsidiaryId });
        savedScheme
            .Producers.Select(x => x.ProducerOrganisationId)
            .Should()
            .BeEquivalentTo(new[] { member1, member2, memberSubsidiary });
        savedDirect
            .Producers.Single(x => x.ProducerOrganisationId == SubmitterId)
            .Materials.Select(x => (x.MaterialCode, x.ObligationTonnes))
            .Should()
            .BeEquivalentTo(new[] { ("PL", 22L), ("GL", 7L), ("GR", 23L) });
        savedDirect
            .Producers.Single(x => x.ProducerOrganisationId == subsidiaryId)
            .Materials.Select(x => (x.MaterialCode, x.ObligationTonnes))
            .Should()
            .BeEquivalentTo(new[] { ("PL", 39L), ("GL", 13L), ("GR", 39L) });
        foreach (var producer in savedScheme.Producers)
        {
            var material = producer.Materials.Should().ContainSingle().Which;
            material.MaterialCode.Should().Be("PL");
            material.ObligationTonnes.Should().Be(12);
        }
        savedScheme.Producers.SelectMany(x => x.Materials).Sum(x => x.ObligationTonnes).Should().Be(36);
    }

    private static ProducerPackaging Producer(Guid id, long plastic, long? glass = null) =>
        new()
        {
            ProducerOrganisationId = id,
            Materials = glass is null
                ? [new() { MaterialCode = "PL", Kilograms = plastic }]
                :
                [
                    new() { MaterialCode = "PL", Kilograms = plastic },
                    new() { MaterialCode = "GL", Kilograms = glass.Value },
                ],
        };

    [Theory]
    [InlineData(ObligationSubmitterType.DirectRegistrant)]
    [InlineData(ObligationSubmitterType.ComplianceScheme)]
    public async Task Process_ShouldCalculateAndSaveProducerMaterials(ObligationSubmitterType type)
    {
        var submission = ApprovedSubmissionFixture
            .Default()
            .With(x => x.SubmitterOrganisationId, SubmitterId)
            .With(x => x.SubmitterType, type)
            .Create();
        var source = Source(submission);

        await Processor(source)
            .Process(ObligationCalculationInstructionFixture.Default().Create(), TestContext.Current.CancellationToken);

        await source.Received(1).Read(2025, TestContext.Current.CancellationToken);
        var saved = await Read(SubmitterId);
        saved.SubmitterType.Should().Be(type);
        saved.ObligationYear.Should().Be(2026);
        var producer = saved.Producers.Should().ContainSingle().Which;
        producer.ProducerOrganisationId.Should().Be(submission.Producers.Single().ProducerOrganisationId);
        producer
            .Materials.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    MaterialCode = ObligationMaterials.Plastic,
                    InputKilograms = 10000L,
                    InputTonnes = 10L,
                    ObligationTonnes = 6L,
                }
            );
    }

    [Fact]
    public async Task Process_WhenSaveCancelsIndependently_ShouldPropagateWithoutProcessingLaterSubmitters()
    {
        var source = Source(
            ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create(),
            ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, Guid.NewGuid()).Create()
        );
        var service = Substitute.For<IObligationCalculationService>();
        var metrics = Substitute.For<IObligationCalculationMetrics>();
        service
            .SaveWithOutcome(Arg.Any<ObligationCalculation>(), Arg.Any<CancellationToken>())
            .Returns<Task<CalculationSaveResult>>(_ => throw new OperationCanceledException());
        var action = () =>
            Processor(source, service, metrics)
                .Process(
                    ObligationCalculationInstructionFixture.Default().Create(),
                    TestContext.Current.CancellationToken
                );

        await action.Should().ThrowAsync<OperationCanceledException>();
        await service.Received(1).SaveWithOutcome(Arg.Any<ObligationCalculation>(), Arg.Any<CancellationToken>());
        metrics.DidNotReceive().SubmitterFailed();
        metrics.DidNotReceive().Submitter(Arg.Any<CalculationSaveOutcome>());
        metrics.DidNotReceive().Instruction(Arg.Any<CalculationInstructionOutcome>());
        _logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Process_ShouldReportActualOutcomesAndIsolatedErrors()
    {
        var metrics = Substitute.For<IObligationCalculationMetrics>();
        var submission = ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create();
        var producer = submission.Producers.Single();
        var material = producer.Materials.Single();
        var changed = submission with
        {
            Producers = [producer with { Materials = [material with { Kilograms = 20000 }] }],
        };
        var invalid = submission with
        {
            Producers = [producer with { Materials = [material with { MaterialCode = "INVALID" }] }],
        };
        var instruction = ObligationCalculationInstructionFixture.Default().Create();
        await Processor(Source(submission), metrics: metrics)
            .Process(instruction, TestContext.Current.CancellationToken);
        await Processor(Source(submission), metrics: metrics)
            .Process(instruction, TestContext.Current.CancellationToken);
        await Processor(Source(changed), metrics: metrics).Process(instruction, TestContext.Current.CancellationToken);
        await Processor(Source(invalid), metrics: metrics).Process(instruction, TestContext.Current.CancellationToken);

        metrics.Received(1).Submitter(CalculationSaveOutcome.Created);
        metrics.Received(1).Submitter(CalculationSaveOutcome.Unchanged);
        metrics.Received(1).Submitter(CalculationSaveOutcome.Updated);
        metrics.Received(1).SubmitterFailed();
        metrics.Received(3).Instruction(CalculationInstructionOutcome.Success);
        metrics.Received(1).Instruction(CalculationInstructionOutcome.SubmitterErrors);
    }

    [Fact]
    public async Task Process_ShouldOnlySaveRequestedSubmitter()
    {
        var otherId = Guid.NewGuid();
        var source = Source(
            ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, otherId).Create(),
            ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create()
        );

        var metrics = Substitute.For<IObligationCalculationMetrics>();
        await Processor(source, metrics: metrics)
            .Process(
                ObligationCalculationInstructionFixture.Default().With(x => x.SubmitterId, SubmitterId).Create(),
                TestContext.Current.CancellationToken
            );
        metrics.Received(1).Submitter(CalculationSaveOutcome.Created);
        metrics.Received(1).Instruction(CalculationInstructionOutcome.Success);

        (await Read(SubmitterId)).Producers.Should().ContainSingle();
        (
            await ObligationCalculations
                .Find(x => x.SubmitterOrganisationId == otherId)
                .AnyAsync(TestContext.Current.CancellationToken)
        )
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_WhenNoDataOrNoMatch_ShouldKeepPreviousCalculation(bool targeted)
    {
        await Processor(
                Source(ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create())
            )
            .Process(ObligationCalculationInstructionFixture.Default().Create(), TestContext.Current.CancellationToken);
        var before = await Read(SubmitterId);
        var source = targeted
            ? Source(ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, Guid.NewGuid()).Create())
            : Source();

        var metrics = Substitute.For<IObligationCalculationMetrics>();
        await Processor(source, metrics: metrics)
            .Process(
                ObligationCalculationInstructionFixture
                    .Default()
                    .With(x => x.SubmitterId, targeted ? SubmitterId : (Guid?)null)
                    .Create(),
                TestContext.Current.CancellationToken
            );
        metrics.DidNotReceive().Submitter(Arg.Any<CalculationSaveOutcome>());
        metrics.DidNotReceive().SubmitterFailed();
        metrics.Received(1).Instruction(CalculationInstructionOutcome.Success);

        (await Read(SubmitterId)).Should().BeEquivalentTo(before);
        _logger
            .Entries.Should()
            .Contain(x => x.Level == LogLevel.Information && x.Message.Contains("No packaging data"));
    }

    [Fact]
    public async Task Process_WhenSubmitterDisappears_ShouldRetainItsPreviousCalculation()
    {
        await Processor(
                Source(ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create())
            )
            .Process(ObligationCalculationInstructionFixture.Default().Create(), TestContext.Current.CancellationToken);
        var before = await Read(SubmitterId);
        var laterId = Guid.NewGuid();

        await Processor(
                Source(ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, laterId).Create())
            )
            .Process(ObligationCalculationInstructionFixture.Default().Create(), TestContext.Current.CancellationToken);

        (await Read(SubmitterId)).Should().BeEquivalentTo(before);
        (await Read(laterId)).ObligationYear.Should().Be(2026);
    }

    [Fact]
    public async Task Process_WhenCalculationFails_ShouldPreservePreviousAndContinue()
    {
        await Processor(
                Source(ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create())
            )
            .Process(ObligationCalculationInstructionFixture.Default().Create(), TestContext.Current.CancellationToken);
        var before = await Read(SubmitterId);
        var laterId = Guid.NewGuid();
        var instruction = ObligationCalculationInstructionFixture.Default().Create();

        var invalidSubmission = ApprovedSubmissionFixture
            .Default()
            .With(x => x.SubmitterOrganisationId, SubmitterId)
            .With(
                x => x.Producers,
                [
                    ProducerPackagingFixture
                        .Default()
                        .With(
                            x => x.Materials,
                            [MaterialWeightFixture.Default().With(x => x.MaterialCode, "INVALID").Create()]
                        )
                        .Create(),
                ]
            )
            .Create();

        await Processor(
                Source(
                    invalidSubmission,
                    ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, laterId).Create()
                )
            )
            .Process(instruction, TestContext.Current.CancellationToken);

        (await Read(SubmitterId)).Should().BeEquivalentTo(before);
        (await Read(laterId)).ObligationYear.Should().Be(2026);
        _logger
            .Entries.Should()
            .ContainSingle(x =>
                x.Level == LogLevel.Error
                && x.Message.Contains(SubmitterId.ToString())
                && x.Message.Contains(instruction.Id.ToString())
                && x.Message.Contains("2026")
            );
    }

    [Fact]
    public async Task Process_WhenSaveFails_ShouldPreservePreviousAndSaveLaterSubmitter()
    {
        var name = $"packaging_save_failure_{Guid.NewGuid():N}";
        await GetMongoDatabase()
            .CreateCollectionAsync(
                name,
                new CreateCollectionOptions<ObligationCalculation>
                {
                    Validator = Builders<ObligationCalculation>.Filter.Eq(x => x.Version, 1),
                },
                TestContext.Current.CancellationToken
            );
        var collection = GetMongoApplicationDatabase().GetCollection<ObligationCalculation>(name);
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<ObligationCalculation>(
                Builders<ObligationCalculation>
                    .IndexKeys.Ascending(x => x.SubmitterOrganisationId)
                    .Ascending(x => x.ObligationYear),
                new CreateIndexOptions { Unique = true }
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );
        try
        {
            var real = Context();
            var context = Substitute.For<IDbContext>();
            context.ObligationCalculations.Returns(collection);
            context.ObligationCalculationHistory.Returns(real.ObligationCalculationHistory);
            context
                .ExecuteTransaction(
                    Arg.Any<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(),
                    Arg.Any<string>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(call =>
                    real.ExecuteTransaction(
                        call.ArgAt<Func<IClientSessionHandle, CancellationToken, Task<ObligationCalculation>>>(0),
                        call.ArgAt<string>(1),
                        call.ArgAt<CancellationToken>(2)
                    )
                );
            var service = new ObligationCalculationService(context, CreateAuditEventService());
            var metrics = Substitute.For<IObligationCalculationMetrics>();
            var first = Processor(
                Source(ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create()),
                service,
                metrics
            );
            await first.Process(
                ObligationCalculationInstructionFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );
            var before = await collection
                .Find(x => x.SubmitterOrganisationId == SubmitterId && x.ObligationYear == 2026)
                .SingleAsync(TestContext.Current.CancellationToken);
            var laterId = Guid.NewGuid();

            var changedSubmission = ApprovedSubmissionFixture
                .Default()
                .With(x => x.SubmitterOrganisationId, SubmitterId)
                .With(
                    x => x.Producers,
                    [
                        ProducerPackagingFixture
                            .Default()
                            .With(
                                x => x.Materials,
                                [MaterialWeightFixture.Default().With(x => x.Kilograms, 20000L).Create()]
                            )
                            .Create(),
                    ]
                )
                .Create();

            await Processor(
                    Source(
                        changedSubmission,
                        ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, laterId).Create()
                    ),
                    service,
                    metrics
                )
                .Process(
                    ObligationCalculationInstructionFixture.Default().Create(),
                    TestContext.Current.CancellationToken
                );

            (
                await collection
                    .Find(x => x.SubmitterOrganisationId == SubmitterId && x.ObligationYear == 2026)
                    .SingleAsync(TestContext.Current.CancellationToken)
            )
                .Should()
                .BeEquivalentTo(before);
            (
                await collection
                    .Find(x => x.SubmitterOrganisationId == laterId && x.ObligationYear == 2026)
                    .SingleAsync(TestContext.Current.CancellationToken)
            )
                .Producers.Single()
                .Materials.Single()
                .ObligationTonnes.Should()
                .Be(6);
            metrics.Received(2).Submitter(CalculationSaveOutcome.Created);
            metrics.DidNotReceive().Submitter(CalculationSaveOutcome.Updated);
            metrics.Received(1).SubmitterFailed();
            metrics.Received(1).Instruction(CalculationInstructionOutcome.SubmitterErrors);
            _logger
                .Entries.Should()
                .ContainSingle(x => x.Level == LogLevel.Error && x.Message.Contains(SubmitterId.ToString()));
        }
        finally
        {
            await GetMongoDatabase().DropCollectionAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Process_WhenCancelledDuringFetch_ShouldPropagateCancellationWithoutSaving()
    {
        using var cancellation = new CancellationTokenSource();
        var source = Substitute.For<IWastePackagingService>();
        source
            .Read(2025, cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();

                return new PackagingResponse
                {
                    Items =
                    [
                        ApprovedSubmissionFixture.Default().With(x => x.SubmitterOrganisationId, SubmitterId).Create(),
                    ],
                };
            });
        Func<Task> process = () =>
            Processor(source).Process(ObligationCalculationInstructionFixture.Default().Create(), cancellation.Token);

        await process.Should().ThrowAsync<OperationCanceledException>();
        (
            await ObligationCalculations.CountDocumentsAsync(
                FilterDefinition<ObligationCalculation>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
        _logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
    }

    private Task<ObligationCalculation> Read(Guid id, int year = 2026) =>
        ObligationCalculations
            .Find(x => x.SubmitterOrganisationId == id && x.ObligationYear == year)
            .SingleAsync(TestContext.Current.CancellationToken);

    private ObligationCalculationProcessor Processor(
        IWastePackagingService source,
        IObligationCalculationService? service = null,
        IObligationCalculationMetrics? metrics = null
    ) =>
        new(
            source,
            new ObligationCalculator(
                Options.Create(
                    new ObligationCalculationOptions
                    {
                        Targets = new()
                        {
                            [2025] = new()
                            {
                                ["PL"] = 0.55,
                                ["GL"] = 0.74,
                                ["GR"] = 0.75,
                            },
                            [2026] = new() { [ObligationMaterials.Plastic] = 0.57 },
                        },
                    }
                ),
                TimeProvider.System
            ),
            service ?? new ObligationCalculationService(Context(), CreateAuditEventService()),
            metrics ?? Substitute.For<IObligationCalculationMetrics>(),
            TimeProvider.System,
            _logger
        );

    private static MongoDbContext Context() =>
        new(GetMongoApplicationDatabase(), Options.Create(new MongoDbOptions()), NullLogger<MongoDbContext>.Instance);

    private static IWastePackagingService Source(params ApprovedSubmission[] items)
    {
        var source = Substitute.For<IWastePackagingService>();
        source.Read(2024, Arg.Any<CancellationToken>()).Returns(new PackagingResponse { Items = items });
        source.Read(2025, Arg.Any<CancellationToken>()).Returns(new PackagingResponse { Items = items });

        return source;
    }
}
