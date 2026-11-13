using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using WireMock.Client.Extensions;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class StartObligationCalculationTests : IntegrationTestBase
{
    [Fact]
    public async Task WhenProcessingIsDisabled_ShouldRejectWithoutPersistingInstruction()
    {
        using var client = CreateClient();
        var response = await client.PostAsync(
            "/admin/obligation-calculations?obligationYear=2026",
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
        var mapping = WireMockContext.WireMockAdminApi.GetMappingBuilder();
        mapping.Given(x =>
            x.WithRequest(r => r.UsingGet().WithPath("/packaging/2025/producer-material-totals-by-submitter"))
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK)
                        .WithBodyAsJson(
                            new
                            {
                                items = new[]
                                {
                                    new
                                    {
                                        submitterOrganisationId = submitterId,
                                        submitterType = "ComplianceScheme",
                                        producers = new[]
                                        {
                                            new
                                            {
                                                producerOrganisationId = producerId,
                                                materials = new[] { new { materialCode = "PL", kilograms = 10000L } },
                                            },
                                        },
                                    },
                                },
                            }
                        )
                )
        );
        var database = GetMongoDatabase().Client.GetDatabase("waste-obligations-scheduled-job-test");
        var instructions = database.GetCollection<ObligationCalculationInstruction>(
            ObligationCalculationInstruction.CollectionName
        );
        var calculations = database.GetCollection<ObligationCalculation>(nameof(ObligationCalculation));

        var registered = await mapping.BuildAndPostAsync(TestContext.Current.CancellationToken);
        try
        {
            registered.Guid.Should().NotBeNull();
            using var client = CreateClient();
            client.BaseAddress = new Uri("http://localhost:8081");
            var response = await client.PostAsync(
                $"/admin/obligation-calculations?obligationYear={obligationYear}&submitterId={submitterId}",
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
        finally
        {
            try
            {
                await Task.WhenAll(
                    instructions.DeleteOneAsync(
                        x => x.SubmitterId == submitterId && x.ObligationYear == obligationYear,
                        CancellationToken.None
                    ),
                    calculations.DeleteOneAsync(
                        x => x.SubmitterOrganisationId == submitterId && x.ObligationYear == obligationYear,
                        CancellationToken.None
                    )
                );
            }
            finally
            {
                using var admin = new HttpClient();
                using var deleted = await admin.DeleteAsync(
                    $"http://localhost:9090/__admin/mappings/{registered.Guid}",
                    CancellationToken.None
                );
                deleted.EnsureSuccessStatusCode();
            }
        }
    }
}
