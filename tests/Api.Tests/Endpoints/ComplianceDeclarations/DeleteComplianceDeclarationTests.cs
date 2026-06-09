using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.ComplianceDeclarations;

public class DeleteComplianceDeclarationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IComplianceDeclarationService ComplianceDeclarationService { get; } =
        Substitute.For<IComplianceDeclarationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IComplianceDeclarationService>(_ => ComplianceDeclarationService);
    }

    [Fact]
    public async Task WhenReadOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(ObjectId.GenerateNewId().ToString()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WhenNotFound_ShouldBeNotFound()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenDeleted_ShouldBeNoContent()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var client = CreateClient(testUser: TestUser.WriteOnly);
        ComplianceDeclarationService.Delete(id, Arg.Any<CancellationToken>()).Returns(true);

        var response = await client.DeleteAsync(
            Testing.Endpoints.ComplianceDeclarations.Delete(id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ComplianceDeclarationService.Received(1).Delete(id, Arg.Any<CancellationToken>());
    }
}
