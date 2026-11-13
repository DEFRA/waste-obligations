using System.Net;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
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
        var database = GetMongoDatabase().Client.GetDatabase("waste-obligations-scheduled-job-test");
        var instructions = database.GetCollection<ObligationCalculationInstruction>(
            ObligationCalculationInstruction.CollectionName
        );
        var calculations = database.GetCollection<ObligationCalculation>(nameof(ObligationCalculation));

        await instructions.DeleteManyAsync(
            FilterDefinition<ObligationCalculationInstruction>.Empty,
            TestContext.Current.CancellationToken
        );
        await calculations.DeleteManyAsync(
            FilterDefinition<ObligationCalculation>.Empty,
            TestContext.Current.CancellationToken
        );
        await database
            .GetCollection<ObligationCalculationHistory>(nameof(ObligationCalculationHistory))
            .DeleteManyAsync(
                FilterDefinition<ObligationCalculationHistory>.Empty,
                TestContext.Current.CancellationToken
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
                var instruction = await instructions
                    .Find(x => x.Id == instructionId)
                    .SingleAsync(TestContext.Current.CancellationToken);
                instruction.Status.Should().Be(ObligationCalculationInstructionStatus.Completed);
                instruction.ObligationYear.Should().Be(obligationYear);
                instruction.SubmitterId.Should().Be(submitterId);
                instruction.TraceId.Should().Be(TraceId);
            },
            timeout: 30,
            delay: TimeSpan.FromMilliseconds(200)
        );

        var calculation = await calculations
            .Find(x => x.SubmitterOrganisationId == submitterId && x.ObligationYear == obligationYear)
            .SingleAsync(TestContext.Current.CancellationToken);
        calculation.SubmitterType.Should().Be(ObligationSubmitterType.ComplianceScheme);
        var producer = calculation.Producers.Should().ContainSingle().Which;
        producer.ProducerOrganisationId.Should().Be(producerId);
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
}
