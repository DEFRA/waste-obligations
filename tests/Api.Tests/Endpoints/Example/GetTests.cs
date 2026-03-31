using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Authentication;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Example;

public class GetTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Theory]
    [InlineData(AclOptions.ClientType.ApiKey)]
    [InlineData(AclOptions.ClientType.OAuth)]
    public async Task WhenGet_ShouldBeOk(AclOptions.ClientType clientType)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly, clientType: clientType);
        var id = Guid.NewGuid();

        var response = await client.GetStringAsync(
            Testing.Endpoints.Example.Get(id),
            TestContext.Current.CancellationToken
        );

        response.Should().Contain(id.ToString());
    }

    [Fact]
    public async Task WhenGet_AndBadRequest_ShouldBeBadRequest()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);
        var id = Guid.NewGuid();

        var response = await client.GetAsync(
            Testing.Endpoints.Example.Get(id, badRequest: true),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
