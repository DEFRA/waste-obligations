using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WasteOrganisations;

namespace Defra.WasteObligations.Api.Tests.Services.WasteOrganisations;

public class WasteOrganisationsOptionsTests
{
    [Theory]
    [InlineData("http", 1)]
    [InlineData("https", 2)]
    public void Configure_WhenScheme_ShouldBeExpectedVersion(string scheme, int expectedVersion)
    {
        var subject = new WasteOrganisationsOptions
        {
            BaseAddress = $"{scheme}://waste-organisations",
            ClientId = "client_id",
            ClientSecret = "client_secret",
        };

        var client = new HttpClient();
        subject.Configure(client);

        switch (expectedVersion)
        {
            case 1:
                client.DefaultRequestVersion.Should().Be(HttpVersion.Version11);
                break;
            case 2:
                client.DefaultRequestVersion.Should().Be(HttpVersion.Version20);
                break;
        }
    }

    [Fact]
    public void BasicAuthCredential_AsExpected()
    {
        var subject = new WasteOrganisationsOptions
        {
            BaseAddress = "https://waste-organisations",
            ClientId = "client_id",
            ClientSecret = "client_secret",
        };

        subject.BasicAuthCredential.Should().Be("Y2xpZW50X2lkOmNsaWVudF9zZWNyZXQ=");
    }
}
