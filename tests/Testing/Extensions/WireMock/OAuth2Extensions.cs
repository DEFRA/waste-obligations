using System.Net;
using AnyOfTypes;
using AwesomeAssertions;
using WireMock.Admin.Mappings;
using WireMock.Client;
using WireMock.Client.Extensions;
using WireMock.Matchers;
using WireMock.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Defra.WasteObligations.Testing.Extensions.WireMock;

public static class OAuth2Extensions
{
    public const string AccessToken = "access_token";

    public static void StubTokenRequest(
        this WireMockServer wireMock,
        string accessToken = AccessToken,
        int expiryInSeconds = 3600,
        string? scope = "scope"
    )
    {
        AnyOf<string, StringPattern>[] patterns =
        [
            "grant_type=client_credentials",
            "client_id=client_id",
            "client_secret=client_secret",
        ];

        if (scope is not null)
            patterns = patterns.Append($"scope={scope}").ToArray();

        wireMock
            .Given(
                Request
                    .Create()
                    .UsingPost()
                    .WithPath("/token")
                    .WithHeader("Content-Type", "application/x-www-form-urlencoded")
                    .WithBody(new FormUrlEncodedMatcher(patterns, matchOperator: MatchOperator.And))
            )
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithBodyAsJson(new { access_token = accessToken, expires_in = expiryInSeconds })
            );
    }

    public static async Task StubTokenRequest(
        this IWireMockAdminApi wireMock,
        string accessToken = AccessToken,
        int expiryInSeconds = 3600,
        string? scope = "scope",
        string? clientId = "client_id"
    )
    {
        var builder = wireMock.GetMappingBuilder();

        object[] patterns = ["grant_type=client_credentials", $"client_id={clientId}", "client_secret=client_secret"];

        if (scope is not null)
            patterns = patterns.Append($"scope={scope}").ToArray();

        builder.Given(x =>
            x.WithRequest(r =>
                    r.UsingPost()
                        .WithPath("/oauth2/v2.0/token")
                        .WithHeader("Content-Type", "application/x-www-form-urlencoded")
                        .WithBody(() =>
                            new BodyModel
                            {
                                Matchers = patterns
                                    .Select(p => new MatcherModel
                                    {
                                        Name = "FormUrlEncodedMatcher",
                                        Pattern = p,
                                        MatchOperator = "And",
                                    })
                                    .ToArray(),
                                MatchOperator = "And",
                            }
                        )
                )
                .WithResponse(r =>
                    r.WithStatusCode(HttpStatusCode.OK)
                        .WithBodyAsJson(new { access_token = accessToken, expires_in = expiryInSeconds })
                )
        );

        var status = await builder.BuildAndPostAsync(TestContext.Current.CancellationToken);
        status.Guid.Should().NotBeNull();
    }
}
