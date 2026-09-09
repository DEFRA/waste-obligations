using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Authentication;
using Defra.WasteObligations.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteObligations.Api.Tests.Endpoints.Admin;

public class AdminAuthorizationTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    public static TheoryData<string, string> AdminRequests =>
        new()
        {
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.AuditEvents() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.AuditEventCounter() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.OrganisationComplianceDeclarations(OrganisationId) },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.OrganisationObligationSummaries(OrganisationId) },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.UnsubmittedPollingStatus() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.UnsubmittedPollingVolume() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.UnsubmittedPollingPlan() },
            { HttpMethod.Post.Method, Testing.Endpoints.Admin.UnsubmittedHistoricalBackfill() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.UnsubmittedReferenceResolutionIssues() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.UnsubmittedOrganisationDetails(OrganisationId) },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.OrganisationEligibilitySnapshot() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.OrganisationEligibility() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.FailedOrganisationObligationSummaries(2026) },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.OrganisationObligationHistoricalBackfills() },
            { HttpMethod.Get.Method, Testing.Endpoints.Admin.OrganisationObligationRequestPacingState() },
        };

    [Theory]
    [MemberData(nameof(AdminRequests))]
    public async Task WhenReadWriteUserRequestsAdminEndpoint_ShouldBeForbidden(string method, string path)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public void AllAdminEndpoints_ShouldRequireAdminPolicy()
    {
        const string adminPathPrefix = "/admin/";

        var adminEndpoints = Factory
            .Services.GetServices<EndpointDataSource>()
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(x => x.RoutePattern.RawText?.StartsWith(adminPathPrefix, StringComparison.Ordinal) is true)
            .ToList();

        adminEndpoints.Should().NotBeEmpty();
        adminEndpoints
            .Should()
            .AllSatisfy(x => x.Metadata.OfType<IAuthorizeData>().Should().Contain(y => y.Policy == PolicyNames.Admin));
    }

    private static Guid OrganisationId => Guid.Parse("3a67e998-ebd2-4dcc-9982-a12ef6db10a5");
}
