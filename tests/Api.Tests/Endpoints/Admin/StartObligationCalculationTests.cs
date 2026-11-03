using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.ObligationCalculations;
using Defra.WasteObligations.Api.Services.Scheduling;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class StartObligationCalculationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IObligationCalculationInstructionService Instructions { get; } =
        Substitute.For<IObligationCalculationInstructionService>();

    private bool ProcessingEnabled { get; set; } = true;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IObligationCalculationInstructionService>(_ => Instructions);
        var options = Substitute.For<IOptionsMonitor<ScheduledJobProcessingOptions>>();
        options
            .Get("ObligationCalculation")
            .Returns(_ => new ScheduledJobProcessingOptions { Enabled = ProcessingEnabled });
        services.AddSingleton(options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenAdmin_ShouldCreateInstruction(bool includeSubmitter)
    {
        var submitterId = Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
        Guid? selectedSubmitter = includeSubmitter ? submitterId : null;
        Instructions
            .Create(2026, selectedSubmitter, Arg.Any<CancellationToken>())
            .Returns(
                new ObligationCalculationInstruction
                {
                    Id = ObjectId.Parse("68bc00000000000000000001"),
                    ObligationYear = 2026,
                    SubmitterId = selectedSubmitter,
                    CreatedAt = new DateTime(2026, 9, 6, 8, 0, 0, DateTimeKind.Utc),
                }
            );
        var client = CreateClient(testUser: TestUser.Admin);
        var response = await client.PostAsync(
            $"/admin/obligation-calculations?obligationYear=2026{(includeSubmitter ? $"&submitterId={submitterId}" : "")}",
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .UseParameters(includeSubmitter);
        await Instructions.Received(1).Create(2026, selectedSubmitter, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?obligationYear=1")]
    [InlineData("?obligationYear=2026&submitterId=invalid")]
    public async Task WhenInvalid_ShouldBeBadRequest(string query)
    {
        var client = CreateClient(testUser: TestUser.Admin);
        var response = await client.PostAsync(
            $"/admin/obligation-calculations{query}",
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await Instructions.DidNotReceiveWithAnyArgs().Create(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WhenProcessingIsDisabled_ShouldReturnConflictWithoutCreatingInstruction()
    {
        ProcessingEnabled = false;
        var client = CreateClient(testUser: TestUser.Admin);
        var response = await client.PostAsync(
            "/admin/obligation-calculations?obligationYear=2026",
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        await Instructions.DidNotReceiveWithAnyArgs().Create(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WhenNotAdmin_ShouldBeForbidden()
    {
        var client = CreateClient();
        var response = await client.PostAsync(
            "/admin/obligation-calculations?obligationYear=2026",
            null,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
