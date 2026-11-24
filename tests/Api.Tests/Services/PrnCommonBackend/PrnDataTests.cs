using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;

namespace Defra.WasteObligations.Api.Tests.Services.PrnCommonBackend;

public class PrnDataTests
{
    [Fact]
    public void StatusUpdatedOn_ShouldUseTheCommonBackendPropertyName()
    {
        var json = JsonSerializer.Serialize(PrnDataFixture.Default().Create());

        using var document = JsonDocument.Parse(json);

        document
            .RootElement.GetProperty("statusUpdatedOn")
            .GetDateTime()
            .Should()
            .Be(new DateTime(2026, 1, 15, 10, 4, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void StatusUpdatedOn_WhenTheCommonBackendOmitsIt_ShouldBeNull()
    {
        var payload = JsonNode.Parse(JsonSerializer.Serialize(PrnDataFixture.Default().Create()))!.AsObject();
        payload.Remove("statusUpdatedOn");

        var result = payload.Deserialize<PrnData>();

        result.Should().NotBeNull();
        result.StatusUpdatedOn.Should().BeNull();
    }

    [Fact]
    public void StatusUpdatedOn_ShouldBeReadFromTheCommonBackendPayload()
    {
        var payload = JsonNode.Parse(JsonSerializer.Serialize(PrnDataFixture.Default().Create()))!.AsObject();
        payload["statusUpdatedOn"] = "2026-11-20T17:28:17.7155444";

        var result = payload.Deserialize<PrnData>();

        result.Should().NotBeNull();
        result
            .StatusUpdatedOn.Should()
            .Be(new DateTime(2026, 11, 20, 17, 28, 17, 715, DateTimeKind.Unspecified).AddTicks(5444));
    }
}
