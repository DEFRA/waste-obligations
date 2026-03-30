using AwesomeAssertions;
using Defra.WasteObligations.Api.Authentication;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Example;

public class PutTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Theory]
    [InlineData(AclOptions.ClientType.ApiKey)]
    [InlineData(AclOptions.ClientType.OAuth)]
    public async Task WhenGet_ShouldBeOk(AclOptions.ClientType clientType)
    {
        var client = CreateClient(testUser: TestUser.WriteOnly, clientType: clientType);
        var id = Guid.NewGuid();

        var response = await client.PutAsync(
            Testing.Endpoints.Example.Put(id),
            null,
            TestContext.Current.CancellationToken
        );

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should()
            .Contain(id.ToString());
    }
}
