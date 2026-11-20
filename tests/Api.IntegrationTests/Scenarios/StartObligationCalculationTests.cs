using System.Net;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.WastePackaging;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Defra.WasteObligations.Testing.Fixtures.WastePackaging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class StartObligationCalculationTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenProcessingIsDisabled_ShouldRejectWithoutPersistingInstruction()
    {
        using var client = CreateClient();
        var response = await client.PostAsync(
            Testing.Endpoints.Admin.ObligationCalculations(
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(2026))
            ),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (
            await ObligationCalculationInstructions.CountDocumentsAsync(
                FilterDefinition<ObligationCalculationInstruction>.Empty,
                cancellationToken: TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task AdminInstruction_MultipleSubmittersAndProducers_PreservesGroupingThroughPackagingHttp()
    {
        const int obligationYear = 2026;
        var directId = Guid.NewGuid();
        var directSubsidiaryId = Guid.NewGuid();
        var schemeId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var memberSubsidiaryId = Guid.NewGuid();
        var response = new PackagingResponse
        {
            Items =
            [
                ApprovedSubmissionFixture
                    .Default()
                    .With(x => x.SubmitterOrganisationId, directId)
                    .With(x => x.SubmitterType, ObligationSubmitterType.DirectRegistrant)
                    .With(
                        x => x.Producers,
                        [Producer(directId, 10_000, 20_000), Producer(directSubsidiaryId, 30_000, 40_000)]
                    )
                    .Create(),
                ApprovedSubmissionFixture
                    .Default()
                    .With(x => x.SubmitterOrganisationId, schemeId)
                    .With(x => x.SubmitterType, ObligationSubmitterType.ComplianceScheme)
                    .With(
                        x => x.Producers,
                        [Producer(memberId, 50_000, 60_000), Producer(memberSubsidiaryId, 70_000, 80_000)]
                    )
                    .Create(),
            ],
        };
        await WireMockContext.WireMockAdminApi.StubWastePackagingRequest(2025, response, traceId: TraceId);

        using var client = CreateClient(ClientType.ScheduledJobs);
        client.DefaultRequestHeaders.Add(TraceHeaderName, TraceId);
        var result = await client.PostAsync(
            Testing.Endpoints.Admin.ObligationCalculations(
                EndpointQuery.New.Where(EndpointFilter.ObligationYear(obligationYear))
            ),
            null,
            TestContext.Current.CancellationToken
        );
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(
            await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        var instructionId = ObjectId.Parse(body.RootElement.GetProperty("id").GetString());
        await WaitForAsync(
            async () =>
            {
                var instruction = await ScheduledJobObligationCalculationInstructions
                    .Find(x => x.Id == instructionId)
                    .SingleAsync(TestContext.Current.CancellationToken);
                instruction.Status.Should().Be(ObligationCalculationInstructionStatus.Completed);
            },
            30,
            TimeSpan.FromMilliseconds(200)
        );

        var calculations = await ScheduledJobObligationCalculations
            .Find(x => x.ObligationYear == obligationYear)
            .ToListAsync(TestContext.Current.CancellationToken);
        calculations.Select(x => x.SubmitterOrganisationId).Should().BeEquivalentTo(new[] { directId, schemeId });
        var direct = calculations.Single(x => x.SubmitterOrganisationId == directId);
        direct.SubmitterType.Should().Be(ObligationSubmitterType.DirectRegistrant);
        direct
            .Producers.Select(x => x.ProducerOrganisationId)
            .Should()
            .BeEquivalentTo(new[] { directId, directSubsidiaryId });
        var scheme = calculations.Single(x => x.SubmitterOrganisationId == schemeId);
        scheme.SubmitterType.Should().Be(ObligationSubmitterType.ComplianceScheme);
        scheme
            .Producers.Select(x => x.ProducerOrganisationId)
            .Should()
            .BeEquivalentTo(new[] { memberId, memberSubsidiaryId });
        calculations
            .SelectMany(x => x.Producers)
            .SelectMany(producer =>
                producer.Materials.Select(material =>
                    (producer.ProducerOrganisationId, material.MaterialCode, material.InputKilograms)
                )
            )
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    (directId, "PL", 10_000L),
                    (directId, "WD", 20_000L),
                    (directSubsidiaryId, "PL", 30_000L),
                    (directSubsidiaryId, "WD", 40_000L),
                    (memberId, "PL", 50_000L),
                    (memberId, "WD", 60_000L),
                    (memberSubsidiaryId, "PL", 70_000L),
                    (memberSubsidiaryId, "WD", 80_000L),
                }
            );
    }

    private static ProducerPackaging Producer(Guid id, long plastic, long wood) =>
        ProducerPackagingFixture
            .Default()
            .With(x => x.ProducerOrganisationId, id)
            .With(
                x => x.Materials,
                [new() { MaterialCode = "PL", Kilograms = plastic }, new() { MaterialCode = "WD", Kilograms = wood }]
            )
            .Create();

    [Fact]
    public async Task AdminInstruction_ShouldPersistCalculationThroughComposeService()
    {
        const int obligationYear = 2026;
        var submitterId = Guid.NewGuid();
        var producerId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWastePackagingRequest(
            2025,
            new PackagingResponse
            {
                Items =
                [
                    ApprovedSubmissionFixture
                        .Default()
                        .With(x => x.SubmitterOrganisationId, submitterId)
                        .With(x => x.SubmitterType, ObligationSubmitterType.ComplianceScheme)
                        .With(
                            x => x.Producers,
                            [
                                ProducerPackagingFixture
                                    .Default()
                                    .With(x => x.ProducerOrganisationId, producerId)
                                    .Create(),
                            ]
                        )
                        .Create(),
                ],
            },
            traceId: TraceId
        );
        using var client = CreateClient(ClientType.ScheduledJobs);
        client.DefaultRequestHeaders.Add(TraceHeaderName, TraceId);
        var response = await client.PostAsync(
            Testing.Endpoints.Admin.ObligationCalculations(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(obligationYear))
                    .Where(EndpointFilter.Query("submitterId", submitterId.ToString()))
            ),
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        var instructionId = ObjectId.Parse(body.RootElement.GetProperty("id").GetString());
        await WaitForAsync(
            async () =>
            {
                var instruction = await ScheduledJobObligationCalculationInstructions
                    .Find(x => x.Id == instructionId)
                    .SingleAsync(TestContext.Current.CancellationToken);
                instruction.Status.Should().Be(ObligationCalculationInstructionStatus.Completed);
                instruction.ObligationYear.Should().Be(obligationYear);
                instruction.SubmitterId.Should().Be(submitterId);
                instruction.CorrelationId.Should().Be(TraceId);
            },
            timeout: 30,
            delay: TimeSpan.FromMilliseconds(200)
        );

        var calculation = await ScheduledJobObligationCalculations
            .Find(x => x.SubmitterOrganisationId == submitterId && x.ObligationYear == obligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        calculation.SubmitterType.Should().Be(ObligationSubmitterType.ComplianceScheme);
        var calculationEvent = await ScheduledJobAuditEvents
            .Find(x => x.Entity == "obligation_calculation")
            .SingleAsync(TestContext.Current.CancellationToken);
        calculationEvent.EntityId.Should().Be(calculation.Id.ToString());
        calculationEvent.Actor.Should().Be("service:waste-obligations");
        calculationEvent.TraceId.Should().Be(TraceId);
        var producer = calculation.Producers.Should().ContainSingle().Which;
        producer.ProducerOrganisationId.Should().Be(producerId);
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
}
