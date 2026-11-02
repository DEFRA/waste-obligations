using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class StartObligationCalculationTests : IntegrationTestBase
{
    [Fact]
    public async Task AdminInstruction_ShouldBeProcessedByComposeService()
    {
        const int obligationYear = 2026;
        var submitterId = Guid.NewGuid();
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
        var instructions = GetMongoDatabase()
            .Client.GetDatabase("waste-obligations-scheduled-job-test")
            .GetCollection<ObligationCalculationInstruction>(ObligationCalculationInstruction.CollectionName);

        try
        {
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
        }
        finally
        {
            await instructions.DeleteOneAsync(x => x.Id == instructionId, TestContext.Current.CancellationToken);
        }
    }
}
