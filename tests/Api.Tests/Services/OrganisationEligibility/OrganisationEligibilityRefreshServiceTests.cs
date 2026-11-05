using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.AccountBackend;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Api.Utils.Metrics;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class OrganisationEligibilityRefreshServiceTests
{
    [Theory]
    [InlineData(11000, true)]
    [InlineData(121, false)]
    public async Task Refresh_WhenInitialSnapshotCommandFails_ShouldClassifyOnlyDuplicateKeysAsConflicts(
        int errorCode,
        bool isConflict
    )
    {
        var dbContext = Substitute.For<IDbContext>();
        ConfigureEmptyCollection(dbContext.OrganisationEligibilitySnapshots);
        ConfigureEmptyCollection(dbContext.OrganisationObligationSummaries);
        ConfigureEmptyCollection(dbContext.ComplianceDeclarations);
        var session = Substitute.For<IClientSessionHandle>();
        dbContext
            .ExecuteTransaction(
                Arg.Any<Func<IClientSessionHandle, CancellationToken, Task<long>>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
                call.Arg<Func<IClientSessionHandle, CancellationToken, Task<long>>>()(
                    session,
                    call.Arg<CancellationToken>()
                )
            );
        dbContext
            .OrganisationComplianceDeclarationEligibilities.CountDocumentsAsync(
                session,
                Arg.Any<FilterDefinition<OrganisationComplianceDeclarationEligibility>>(),
                Arg.Any<CountOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1L);
        var commandException = new MongoCommandException(
            new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))),
            "Snapshot insert failed",
            new BsonDocument("insert", nameof(OrganisationEligibilitySnapshot)),
            new BsonDocument { { "ok", 0 }, { "code", errorCode } }
        );
        dbContext
            .OrganisationEligibilitySnapshots.InsertOneAsync(
                session,
                Arg.Any<OrganisationEligibilitySnapshot>(),
                Arg.Any<InsertOneOptions>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromException(commandException));
        var organisation = OrganisationFixture.Default().Create();
        var source = Substitute.For<IOrganisationEligibilitySource>();
        source.Search(Arg.Any<CancellationToken>()).Returns(new OrganisationSearch { Organisations = [organisation] });
        var referenceSearch = Substitute.For<IOrganisationReferenceSearchService>();
        referenceSearch
            .SearchOrganisationsByExternalIds(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationsByExternalIdsResponse
                {
                    Organisations =
                    [
                        new AccountOrganisation
                        {
                            ExternalId = organisation.Id.ToString("D"),
                            ReferenceNumber = "051829",
                        },
                    ],
                }
            );
        var options = Options.Create(new OrganisationEligibilityOptions());
        var subject = new OrganisationEligibilityRefreshService(
            dbContext,
            source,
            new OrganisationReferenceResolver(
                referenceSearch,
                options,
                Substitute.For<IOrganisationEligibilityRefreshMetrics>(),
                NullLogger<OrganisationReferenceResolver>.Instance
            ),
            new UnsubmittedEligibilityVisibilityService(dbContext),
            options,
            TimeProvider.System,
            NullLogger<OrganisationEligibilityRefreshService>.Instance
        );

        var exception = await Record.ExceptionAsync(() => subject.Refresh(TestContext.Current.CancellationToken));

        if (isConflict)
        {
            exception
                .Should()
                .BeOfType<OrganisationEligibilityRefreshConflictException>()
                .Which.InnerException.Should()
                .BeSameAs(commandException);
        }
        else
        {
            exception.Should().BeSameAs(commandException);
        }
    }

    private static void ConfigureEmptyCollection<T>(IMongoCollection<T> collection)
    {
        var cursor = Substitute.For<IAsyncCursor<T>>();
        cursor.Current.Returns([]);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(true, false);
        collection
            .FindAsync(Arg.Any<FilterDefinition<T>>(), Arg.Any<FindOptions<T, T>>(), Arg.Any<CancellationToken>())
            .Returns(cursor);
    }
}
