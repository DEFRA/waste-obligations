using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class SearchComplianceDeclarationTests : IntegrationTestBase
{
    private ComplianceDeclarationService Subject { get; } =
        new(
            new MongoDbContext(GetMongoDatabase()),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System
        );

    [Fact]
    public async Task Search_WhenPaginationIsUsed_ShouldReturnAllResults()
    {
        const int recordCount = 5;
        const int pageSize = 2;
        var seededIds = new List<string>();

        for (var i = 0; i < recordCount; i++)
        {
            var entity = await Subject.Create(
                ComplianceDeclarationFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );
            seededIds.Add(entity.Id.ToString());
        }

        var client = CreateClient();
        var collectedDeclarations = new List<ComplianceDeclaration>();
        var currentPage = 1;
        int totalCount;

        do
        {
            var query = EndpointQuery
                .New.Where(EndpointFilter.Page(currentPage))
                .Where(EndpointFilter.PageSize(pageSize));

            var response = await client.GetFromJsonAsync<ComplianceDeclarationsPaged>(
                Testing.Endpoints.ComplianceDeclarations.Search(query),
                TestContext.Current.CancellationToken
            );

            if (response == null)
                break;

            totalCount = response.Total;
            collectedDeclarations.AddRange(response.ComplianceDeclarations);
            currentPage++;
        } while (collectedDeclarations.Count < totalCount);

        collectedDeclarations.Should().HaveCount(recordCount);
        collectedDeclarations.Select(x => x.Id).Should().BeEquivalentTo(seededIds);
    }
}
