using System.Net.Http.Json;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Api.Utils.Http;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Authentication;
using Defra.WasteObligations.Testing.Extensions.WireMock;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;
using Registration = Defra.WasteObligations.Api.Services.WasteOrganisations.Registration;
using WasteOrganisationsAddress = Defra.WasteObligations.Api.Services.WasteOrganisations.Address;
using WasteOrganisationsRegistrationStatus = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationStatus;
using WasteOrganisationsRegistrationType = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationType;

namespace Defra.WasteObligations.Api.IntegrationTests.Scenarios;

public class RefreshOrganisationEligibilityFromDownstreamServicesTests : IntegrationTestBase
{
    [Fact]
    public async Task Refresh_OutsideAnHttpRequest_ShouldHydrateAndExposeResolvedOrganisations()
    {
        var firstOrganisationId = Guid.NewGuid();
        var secondOrganisationId = Guid.NewGuid();
        await WireMockContext.WireMockAdminApi.StubWasteOrganisationsSearchRequest(
            BasicAuthCredential.ForClient(ClientIds.WasteOrganisations),
            new OrganisationSearch
            {
                Organisations =
                [
                    SourceOrganisation(firstOrganisationId, "Alpha Packaging", 2025, 2026),
                    SourceOrganisation(secondOrganisationId, "Zeta Packaging", 2026),
                ],
            }
        );
        await WireMockContext.WireMockAdminApi.StubTokenRequest(clientId: ClientIds.AccountBackend);
        await WireMockContext.WireMockAdminApi.StubAccountBackendOrganisationsByExternalIdsRequest(
            OAuth2Extensions.AccessToken,
            new OrganisationsByExternalIdsResponse
            {
                Organisations =
                [
                    new AccountOrganisation
                    {
                        ExternalId = firstOrganisationId.ToString("D"),
                        ReferenceNumber = "100001",
                    },
                    new AccountOrganisation
                    {
                        ExternalId = secondOrganisationId.ToString("D"),
                        ReferenceNumber = "100002",
                    },
                ],
            }
        );
        using var serviceProvider = CreateServiceProvider();
        var subject = CreateSubject(serviceProvider);

        var refresh = await subject.Refresh(TestContext.Current.CancellationToken);

        refresh.Outcome.Should().Be(OrganisationEligibilityRefreshOutcome.Promoted);
        refresh.RowCount.Should().Be(3);
        var rows = await OrganisationEligibilities
            .Find(x => x.Generation == refresh.ActiveGeneration)
            .ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(x => x.Id != ObjectId.Empty);
        rows.Where(x => x.OrganisationId == firstOrganisationId)
            .Should()
            .OnlyContain(x => x.ReferenceNumber == "100001");
        rows.Where(x => x.OrganisationId == secondOrganisationId)
            .Should()
            .ContainSingle()
            .Which.ReferenceNumber.Should()
            .Be("100002");
        var caches = await OrganisationReferenceCaches
            .Find(Builders<OrganisationReferenceCache>.Filter.Empty)
            .ToListAsync(TestContext.Current.CancellationToken);
        caches.Should().HaveCount(2);
        caches.Should().OnlyContain(x => x.Id != ObjectId.Empty);
        caches.Should().OnlyContain(x => x.ResolutionState == OrganisationReferenceNumberResolutionState.Resolved);
        await ComplianceDeclarationReviewStateSnapshots.InsertOneAsync(
            new ComplianceDeclarationReviewStateSnapshot
            {
                Id = ComplianceDeclarationReviewStateSnapshot.SnapshotId,
                BackfillCompletedAt = DateTime.UtcNow,
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        var client = CreateClient();

        var response = await client.GetFromJsonAsync<UnsubmittedComplianceDeclarationsPaged>(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
                    .Where(EndpointFilter.Sort("OrganisationName[asc]"))
            ),
            TestContext.Current.CancellationToken
        );

        response.Should().NotBeNull();
        response!.Total.Should().Be(2);
        response
            .UnsubmittedComplianceDeclarations.Select(x => x.OrganisationId)
            .Should()
            .Equal(firstOrganisationId, secondOrganisationId);
        response
            .UnsubmittedComplianceDeclarations.Select(x => x.OrganisationReferenceNumber)
            .Should()
            .Equal("100001", "100002");
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{WasteOrganisationsOptions.SectionName}:BaseAddress"] = "http://localhost:9090",
                    [$"{WasteOrganisationsOptions.SectionName}:ClientId"] = ClientIds.WasteOrganisations,
                    [$"{WasteOrganisationsOptions.SectionName}:ClientSecret"] = "client_secret",
                    [$"{AccountBackendOptions.SectionName}:BaseAddress"] = "http://localhost:9090",
                    [$"{AccountBackendOptions.SectionName}:TokenEndpoint"] = "http://localhost:9090/oauth2/v2.0/token",
                    [$"{AccountBackendOptions.SectionName}:ClientId"] = ClientIds.AccountBackend,
                    [$"{AccountBackendOptions.SectionName}:ClientSecret"] = "client_secret",
                    [$"{AccountBackendOptions.SectionName}:Scope"] = "scope",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTransient<ProxyHttpMessageHandler>();
        services.AddWasteOrganisationsService(addResiliencePipeline: false);
        services.AddAccountBackendService(addResiliencePipeline: false);
        return services.BuildServiceProvider();
    }

    private OrganisationEligibilityRefreshService CreateSubject(IServiceProvider serviceProvider)
    {
        var dbContext = new MongoDbContext(
            GetMongoDatabase(),
            Options.Create(new MongoDbOptions()),
            NullLogger<MongoDbContext>.Instance
        );
        var cacheService = new OrganisationReferenceCacheService(
            dbContext,
            serviceProvider.GetRequiredService<IOrganisationReferenceSearchService>(),
            Options.Create(new OrganisationEligibilityOptions { AccountReferenceNumberBatchSize = 10 }),
            TimeProvider.System
        );

        return new OrganisationEligibilityRefreshService(
            dbContext,
            serviceProvider.GetRequiredService<IOrganisationEligibilitySource>(),
            cacheService,
            TimeProvider.System
        );
    }

    private static Organisation SourceOrganisation(Guid organisationId, string name, params int[] years) =>
        new()
        {
            Id = organisationId,
            Name = name,
            Address = new WasteOrganisationsAddress(),
            Registrations =
            [
                .. years.Select(year => new Registration
                {
                    Type = WasteOrganisationsRegistrationType.LargeProducer,
                    Status = WasteOrganisationsRegistrationStatus.Registered,
                    RegistrationYear = year,
                }),
            ],
        };
}
