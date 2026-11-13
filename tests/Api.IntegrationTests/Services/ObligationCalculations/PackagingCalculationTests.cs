using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.ScheduledJobs;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Packaging;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services.ObligationCalculations;

public class PackagingCalculationTests : IntegrationTestBase
{
    private static readonly Guid SubmitterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProducerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly RecordingLogger<ObligationCalculationProcessor> _logger = new();

    [Theory]
    [InlineData("DirectRegistrant", ObligationSubmitterType.DirectRegistrant)]
    [InlineData("ComplianceScheme", ObligationSubmitterType.ComplianceScheme)]
    public async Task Process_ShouldCalculateAndSaveMappedProducerMaterials(
        string type,
        ObligationSubmitterType expectedType
    )
    {
        var source = Source(Submission(SubmitterId, type));

        await Processor(source).Process(Instruction(), TestContext.Current.CancellationToken);

        await source.Received(1).GetFullYear(2025, TestContext.Current.CancellationToken);
        var saved = await Read(SubmitterId);
        saved.SubmitterType.Should().Be(expectedType);
        saved.ObligationYear.Should().Be(2026);
        var producer = saved.Producers.Should().ContainSingle().Which;
        producer.ProducerOrganisationId.Should().Be(ProducerId);
        producer
            .Materials.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    MaterialCode = "PL",
                    InputKilograms = 10000L,
                    InputTonnes = 10L,
                    ObligationTonnes = 6L,
                }
            );
    }

    [Fact]
    public async Task Process_ShouldOnlySaveRequestedSubmitter()
    {
        var otherId = Guid.NewGuid();
        var source = Source(Submission(otherId), Submission(SubmitterId));

        await Processor(source).Process(Instruction(SubmitterId), TestContext.Current.CancellationToken);

        (await Read(SubmitterId)).Producers.Should().ContainSingle();
        (
            await Calculations
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
        await Processor(Source(Submission(SubmitterId))).Process(Instruction(), TestContext.Current.CancellationToken);
        var before = await Read(SubmitterId);
        var source = targeted ? Source(Submission(Guid.NewGuid())) : Source();

        await Processor(source)
            .Process(Instruction(targeted ? SubmitterId : null), TestContext.Current.CancellationToken);

        (await Read(SubmitterId)).Should().BeEquivalentTo(before);
        _logger
            .Entries.Should()
            .Contain(x => x.Level == LogLevel.Information && x.Message.Contains("No packaging data"));
    }

    [Fact]
    public async Task Process_WhenSubmitterDisappears_ShouldRetainItsPreviousCalculation()
    {
        await Processor(Source(Submission(SubmitterId))).Process(Instruction(), TestContext.Current.CancellationToken);
        var before = await Read(SubmitterId);
        var laterId = Guid.NewGuid();

        await Processor(Source(Submission(laterId))).Process(Instruction(), TestContext.Current.CancellationToken);

        (await Read(SubmitterId)).Should().BeEquivalentTo(before);
        (await Read(laterId)).ObligationYear.Should().Be(2026);
    }

    [Theory]
    [InlineData("Unknown", "PL")]
    [InlineData("DirectRegistrant", "INVALID")]
    public async Task Process_WhenMappingOrCalculationFails_ShouldPreservePreviousAndContinue(
        string type,
        string material
    )
    {
        await Processor(Source(Submission(SubmitterId))).Process(Instruction(), TestContext.Current.CancellationToken);
        var before = await Read(SubmitterId);
        var laterId = Guid.NewGuid();
        var instruction = Instruction();

        await Processor(Source(Submission(SubmitterId, type, material), Submission(laterId)))
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
    public async Task Process_WhenGroupIsMalformed_ShouldContinueWithLaterSubmitter()
    {
        await Processor(
                Source(
                    JsonSerializer.SerializeToElement(new { submitterOrganisationId = SubmitterId }),
                    Submission(Guid.NewGuid())
                )
            )
            .Process(Instruction(), TestContext.Current.CancellationToken);

        (
            await Calculations.CountDocumentsAsync(
                FilterDefinition<ObligationCalculation>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(1);
        _logger
            .Entries.Should()
            .ContainSingle(x => x.Level == LogLevel.Error && x.Message.Contains(SubmitterId.ToString()));
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
            var service = new ObligationCalculationService(context);
            var first = Processor(Source(Submission(SubmitterId)), service);
            await first.Process(Instruction(), TestContext.Current.CancellationToken);
            var before = await collection
                .Find(x => x.SubmitterOrganisationId == SubmitterId && x.ObligationYear == 2026)
                .SingleAsync(TestContext.Current.CancellationToken);
            var laterId = Guid.NewGuid();

            await Processor(Source(Submission(SubmitterId, kilograms: 20000), Submission(laterId)), service)
                .Process(Instruction(), TestContext.Current.CancellationToken);

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
        var source = Substitute.For<IPackagingSource>();
        source
            .GetFullYear(2025, cancellation.Token)
            .Returns(_ =>
            {
                cancellation.Cancel();

                return new PackagingResponse { Items = [Submission(SubmitterId)] };
            });
        Func<Task> process = () => Processor(source).Process(Instruction(), cancellation.Token);

        await process.Should().ThrowAsync<OperationCanceledException>();
        (
            await Calculations.CountDocumentsAsync(
                FilterDefinition<ObligationCalculation>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
        _logger.Entries.Should().NotContain(x => x.Level == LogLevel.Error);
    }

    private IMongoCollection<ObligationCalculation> Calculations =>
        GetMongoDatabase().GetCollection<ObligationCalculation>(nameof(ObligationCalculation));

    private Task<ObligationCalculation> Read(Guid id) =>
        Calculations
            .Find(x => x.SubmitterOrganisationId == id && x.ObligationYear == 2026)
            .SingleAsync(TestContext.Current.CancellationToken);

    private ObligationCalculationProcessor Processor(
        IPackagingSource source,
        IObligationCalculationService? service = null
    ) =>
        new(
            source,
            new ObligationCalculator(
                Options.Create(
                    new ObligationCalculationOptions { Targets = new() { [2026] = new() { ["PL"] = 0.57 } } }
                ),
                TimeProvider.System
            ),
            service ?? new ObligationCalculationService(Context()),
            _logger
        );

    private static MongoDbContext Context() =>
        new(GetMongoApplicationDatabase(), Options.Create(new MongoDbOptions()), NullLogger<MongoDbContext>.Instance);

    private static ObligationCalculationInstruction Instruction(Guid? id = null) =>
        new()
        {
            ObligationYear = 2026,
            SubmitterId = id,
            CreatedAt = DateTime.UtcNow,
        };

    private static IPackagingSource Source(params JsonElement[] items)
    {
        var source = Substitute.For<IPackagingSource>();
        source.GetFullYear(2025, Arg.Any<CancellationToken>()).Returns(new PackagingResponse { Items = items });

        return source;
    }

    private static JsonElement Submission(
        Guid id,
        string type = "DirectRegistrant",
        string material = "PL",
        long kilograms = 10000
    ) =>
        JsonSerializer.SerializeToElement(
            new
            {
                submitterOrganisationId = id,
                submitterType = type,
                producers = new[]
                {
                    new
                    {
                        producerOrganisationId = ProducerId,
                        materials = new[] { new { materialCode = material, kilograms } },
                    },
                },
            }
        );
}
