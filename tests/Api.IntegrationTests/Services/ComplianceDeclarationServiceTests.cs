using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Utils.Logging;
using Defra.WasteObligations.AuditEvents;
using Defra.WasteObligations.AuditEvents.Data;
using Defra.WasteObligations.AuditEvents.Entities;
using Defra.WasteObligations.Testing.Fakes;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class ComplianceDeclarationServiceTests : IntegrationTestBase
{
    private const string Entity = "compliance_declaration";

    private ComplianceDeclarationService Subject { get; }

    public ComplianceDeclarationServiceTests()
    {
        var database = GetMongoDatabase();
        var auditEventDbContext = new AuditEventDbContext(database);
        var dbContext = new MongoDbContext(database);
        var auditEventService = new AuditEventService(
            auditEventDbContext,
            TimeProvider.System,
            new FakeEventIdGenerator()
        );

        Subject = new(
            dbContext,
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System,
            auditEventService,
            HeaderPropagationValues(),
            Options.Create(new TraceHeader { Name = TraceHeaderName })
        );
    }

    [Fact]
    public async Task Read_WhenNoComplianceDeclaration_ShouldBeNull()
    {
        var complianceDeclaration = await Subject.Read(
            ObjectId.GenerateNewId().ToString(),
            TestContext.Current.CancellationToken
        );

        complianceDeclaration.Should().BeNull();
    }

    [Fact]
    public async Task Create_WhenInserted_ShouldBeFound()
    {
        var initial = await Subject.Create(
            ComplianceDeclarationFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);
        var auditEvent = await AuditEvents
            .Find(x => x.Sequence == 1)
            .SingleAsync(TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(initial);
        auditEvent.EventId.Should().Be("01HXYZ00000000000000000001");
        auditEvent.Entity.Should().Be(Entity);
        auditEvent.EntityId.Should().Be(initial.Id.ToString());
        auditEvent.Operation.Should().Be("insert");
        auditEvent.EventType.Should().Be("submission.created");
        auditEvent.DeletedReason.Should().BeNull();
        auditEvent.Actor.Should().Be("service:waste-obligations");
        auditEvent.Version.Should().Be(1);
        auditEvent.SchemaVersion.Should().Be(ComplianceDeclaration.SchemaVersionValue);
        auditEvent.TraceId.Should().Be(TraceId);
        auditEvent.Before.Should().BeNull();
        auditEvent.After.Should().NotBeNull();
        auditEvent.After!["_id"].Should().Be(initial.Id);
        auditEvent.After["version"].Should().Be(1);
    }

    [Fact]
    public async Task Create_WhenInserted_WithAudit_ShouldBeValidAudit()
    {
        var initial = await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Audit, AuditEntryFixture.SubmittedThenCancelled())
                .Create(),
            TestContext.Current.CancellationToken
        );

        var retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(initial);
    }

    [Fact]
    public async Task Create_WhenAuditEventFails_ShouldAbortTransaction()
    {
        var database = GetMongoDatabase();
        var subject = new ComplianceDeclarationService(
            new MongoDbContext(database),
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System,
            new ThrowingAuditEventService(),
            HeaderPropagationValues(),
            Options.Create(new TraceHeader { Name = TraceHeaderName })
        );
        var complianceDeclaration = ComplianceDeclarationFixture.Default().Create();
        var act = async () => await subject.Create(complianceDeclaration, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ThrowingAuditEventService.Message);

        var retrieved = await Subject.Read(complianceDeclaration.Id.ToString(), TestContext.Current.CancellationToken);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task Read_WhenMatchingData_ShouldReturn()
    {
        var organisationId = Guid.NewGuid();
        const int obligationYear = 2025;

        var result = await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, OrganisationFixture.Organisation().With(x => x.Id, organisationId).Create())
                .With(x => x.ObligationYear, obligationYear)
                .Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, OrganisationFixture.Organisation().With(x => x.Id, organisationId).Create())
                .With(x => x.ObligationYear, obligationYear + 1)
                .Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.ObligationYear, obligationYear).Create(),
            TestContext.Current.CancellationToken
        );

        var complianceDeclarations = (
            await Subject.Read(organisationId, obligationYear, TestContext.Current.CancellationToken)
        ).ToList();

        complianceDeclarations.Should().ContainSingle();
        complianceDeclarations.Should().Contain(x => x.Id == result.Id);
    }

    [Fact]
    public async Task Delete_WhenNoComplianceDeclaration_ShouldReturnFalse()
    {
        var deleted = await Subject.Delete(ObjectId.GenerateNewId().ToString(), TestContext.Current.CancellationToken);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WhenDeleted_ShouldRemove()
    {
        var initial = await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );

        var deleted = await Subject.Delete(initial.Id.ToString(), TestContext.Current.CancellationToken);
        var retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);

        deleted.Should().BeTrue();
        retrieved.Should().BeNull();

        var auditEvents = await AuditEvents
            .Find(FilterDefinition<AuditEvent>.Empty)
            .SortBy(x => x.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditEvents.Should().HaveCount(2);
        auditEvents[1].Sequence.Should().Be(2);
        auditEvents[1].EntityId.Should().Be(initial.Id.ToString());
        auditEvents[1].Operation.Should().Be("delete");
        auditEvents[1].EventType.Should().Be("submission.removed");
        auditEvents[1].DeletedReason.Should().Be("elevated system allowed removal");
        auditEvents[1].Version.Should().Be(2);
        auditEvents[1].TraceId.Should().Be(TraceId);
        auditEvents[1].Before.Should().NotBeNull();
        auditEvents[1].After.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenDatabaseReadPreferenceIsSecondaryPreferred_ShouldRemove()
    {
        var subject = CreateSubject(GetMongoDatabase().WithReadPreference(ReadPreference.SecondaryPreferred));
        var initial = await subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );

        var deleted = await subject.Delete(initial.Id.ToString(), TestContext.Current.CancellationToken);

        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenConcurrent_ShouldFail()
    {
        var current = ComplianceDeclarationFixture.DirectProducer().Create();
        var session = Substitute.For<IClientSessionHandle>();
        var cursor = Substitute.For<IAsyncCursor<ComplianceDeclaration>>();
        cursor.MoveNextAsync(TestContext.Current.CancellationToken).Returns(true, false);
        cursor.Current.Returns([current]);

        var collection = Substitute.For<IMongoCollection<ComplianceDeclaration>>();
        collection
            .FindAsync(
                session,
                Arg.Any<FilterDefinition<ComplianceDeclaration>>(),
                Arg.Any<FindOptions<ComplianceDeclaration, ComplianceDeclaration>>(),
                TestContext.Current.CancellationToken
            )
            .Returns(cursor);
        collection
            .DeleteOneAsync(
                session,
                Arg.Any<FilterDefinition<ComplianceDeclaration>>(),
                Arg.Any<DeleteOptions>(),
                TestContext.Current.CancellationToken
            )
            .Returns(new DeleteResult.Acknowledged(0));

        var dbContext = Substitute.For<IDbContext>();
        dbContext.ComplianceDeclarations.Returns(collection);
        dbContext.StartSession(TestContext.Current.CancellationToken).Returns(session);

        var subject = new ComplianceDeclarationService(
            dbContext,
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System,
            Substitute.For<IAuditEventService>(),
            HeaderPropagationValues(),
            Options.Create(new TraceHeader { Name = TraceHeaderName })
        );
        var act = async () => await subject.Delete(current.Id.ToString(), TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<ConcurrencyException>()
            .WithMessage($"Concurrency issue on delete, compliance declaration with id '{current.Id}' was not deleted");
        await session.Received(1).AbortTransactionAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Delete_WhenAuditEventFails_ShouldAbortTransaction()
    {
        var database = GetMongoDatabase();
        var subject = new ComplianceDeclarationService(
            new MongoDbContext(database),
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System,
            new ThrowingAuditEventService(),
            HeaderPropagationValues(),
            Options.Create(new TraceHeader { Name = TraceHeaderName })
        );
        var initial = await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );
        var act = async () => await subject.Delete(initial.Id.ToString(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ThrowingAuditEventService.Message);

        var retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);
        retrieved.Should().BeEquivalentTo(initial);
    }

    [Fact]
    public async Task Update_WhenUpdated_ShouldChange()
    {
        var initial = await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );
        initial.Version.Should().Be(1);
        initial.Created.Should().Be(initial.Updated).And.NotBe(DateTime.MinValue);

        var retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        var updated = retrieved with { ObligationYear = 2027 };

        retrieved = await Subject.Update(retrieved, updated, TestContext.Current.CancellationToken);
        retrieved.Version.Should().Be(2);
        retrieved.Updated.Should().BeAfter(retrieved.Created);

        retrieved.ObligationYear.Should().Be(2027);

        retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved.ObligationYear.Should().Be(2027);

        var auditEvents = await AuditEvents
            .Find(FilterDefinition<AuditEvent>.Empty)
            .SortBy(x => x.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditEvents.Should().HaveCount(2);
        auditEvents[1].Sequence.Should().Be(2);
        auditEvents[1].EntityId.Should().Be(initial.Id.ToString());
        auditEvents[1].Operation.Should().Be("update");
        auditEvents[1].EventType.Should().Be("submission.amended");
        auditEvents[1].DeletedReason.Should().BeNull();
        auditEvents[1].Version.Should().Be(2);
        auditEvents[1].TraceId.Should().Be(TraceId);
        auditEvents[1].Before.Should().NotBeNull();
        auditEvents[1].Before!["version"].Should().Be(1);
        auditEvents[1].After.Should().NotBeNull();
        auditEvents[1].After!["version"].Should().Be(2);
        auditEvents[1].After!["obligationYear"].Should().Be(2027);
    }

    [Fact]
    public async Task Update_WhenConcurrent_SecondShouldFail()
    {
        var initial = await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );

        var retrieved1 = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);
        var retrieved2 = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);

        retrieved1.Should().NotBeNull();
        retrieved2.Should().NotBeNull();

        var updated1 = retrieved1 with { ObligationYear = 2027 };
        await Subject.Update(retrieved1, updated1, TestContext.Current.CancellationToken);

        var updated2 = retrieved2 with { ObligationYear = 2028 };
        var act = async () => await Subject.Update(retrieved2, updated2, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<ConcurrencyException>()
            .WithMessage($"Concurrency issue on write, compliance declaration with id '{initial.Id}' was not updated");

        var auditEvents = await AuditEvents
            .Find(FilterDefinition<AuditEvent>.Empty)
            .SortBy(x => x.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);

        await Verify(ToVerifyAuditEvents(auditEvents)).ScrubMembers("EntityId", "_id");
    }

    [Fact]
    public async Task Write_WhenMultipleDeclarations_ShouldUseGlobalSequenceAndPerEntityVersion()
    {
        var first = await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );
        var second = await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );

        await Subject.Update(first, first with { ObligationYear = 2027 }, TestContext.Current.CancellationToken);
        await Subject.Update(second, second with { ObligationYear = 2028 }, TestContext.Current.CancellationToken);

        var auditEvents = await AuditEvents
            .Find(FilterDefinition<AuditEvent>.Empty)
            .SortBy(x => x.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);

        await Verify(ToVerifyAuditEvents(auditEvents)).ScrubMembers("EntityId", "_id").DisableDateCounting();
    }

    [Fact]
    public async Task Search_WhenFilteringByObligationYear_ShouldReturnMatchingResults()
    {
        const int targetYear = 2025;
        const int otherYear = 2026;

        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.ObligationYear, targetYear).Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.ObligationYear, targetYear).Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.ObligationYear, otherYear).Create(),
            TestContext.Current.CancellationToken
        );

        var result = await Subject.Search(
            new ComplianceDeclarationSearchQuery { ObligationYear = targetYear },
            1,
            10,
            TestContext.Current.CancellationToken
        );

        result.ComplianceDeclarations.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.ComplianceDeclarations.Should().AllSatisfy(x => x.ObligationYear.Should().Be(targetYear));
    }

    [Fact]
    public async Task Search_WhenFilteringByStatus_ShouldReturnMatchingResults()
    {
        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.Status, ComplianceDeclarationStatus.Submitted).Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.Status, ComplianceDeclarationStatus.Cancelled).Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture.Default().With(x => x.Status, ComplianceDeclarationStatus.Accepted).Create(),
            TestContext.Current.CancellationToken
        );

        var result = await Subject.Search(
            new ComplianceDeclarationSearchQuery
            {
                Status = [ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Cancelled],
            },
            1,
            10,
            TestContext.Current.CancellationToken
        );

        result.ComplianceDeclarations.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result
            .ComplianceDeclarations.Should()
            .AllSatisfy(x =>
                x.Status.Should().BeOneOf(ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Cancelled)
            );
    }

    [Theory]
    [InlineData(new[] { RegistrationType.DirectProducer })]
    [InlineData(new[] { RegistrationType.ComplianceScheme })]
    [InlineData(new[] { RegistrationType.DirectProducer, RegistrationType.ComplianceScheme })]
    public async Task Search_WhenFilteringByRegistrationType_ShouldReturnMatchingResults(
        RegistrationType[] registrationTypes
    )
    {
        await Subject.Create(
            ComplianceDeclarationFixture.DirectProducer().Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture.ComplianceScheme().Create(),
            TestContext.Current.CancellationToken
        );

        var result = await Subject.Search(
            new ComplianceDeclarationSearchQuery { RegistrationType = registrationTypes },
            1,
            10,
            TestContext.Current.CancellationToken
        );

        result.ComplianceDeclarations.Should().HaveCount(registrationTypes.Length);
        result.Total.Should().Be(registrationTypes.Length);
        result
            .ComplianceDeclarations.Should()
            .AllSatisfy(x => x.Organisation.RegistrationType.Should().BeOneOf(registrationTypes));
    }

    [Fact]
    public async Task Search_WhenFilteringByOrganisationName_ShouldBeCaseInsensitive()
    {
        const string name = "Waste Management Ltd";

        await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, OrganisationFixture.Organisation().With(y => y.Name, name).Create())
                .Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(
                    x => x.Organisation,
                    OrganisationFixture.Organisation().With(y => y.Name, name.ToUpper()).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, OrganisationFixture.Organisation().With(y => y.Name, "Other Corp").Create())
                .Create(),
            TestContext.Current.CancellationToken
        );

        var resultLowercase = await Subject.Search(
            new ComplianceDeclarationSearchQuery { OrganisationName = name.ToLower() },
            1,
            10,
            TestContext.Current.CancellationToken
        );
        var resultUppercase = await Subject.Search(
            new ComplianceDeclarationSearchQuery { OrganisationName = name.ToUpper() },
            1,
            10,
            TestContext.Current.CancellationToken
        );

        resultLowercase.ComplianceDeclarations.Should().HaveCount(2);
        resultUppercase.ComplianceDeclarations.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_WhenPaging_ShouldReturnCorrectPageAndTotal()
    {
        const int pageSize = 2;
        for (var i = 0; i < 5; i++)
        {
            await Subject.Create(
                ComplianceDeclarationFixture.Default().Create(),
                TestContext.Current.CancellationToken
            );
        }

        var page1 = await Subject.Search(
            new ComplianceDeclarationSearchQuery(),
            1,
            pageSize,
            TestContext.Current.CancellationToken
        );
        var page2 = await Subject.Search(
            new ComplianceDeclarationSearchQuery(),
            2,
            pageSize,
            TestContext.Current.CancellationToken
        );
        var page3 = await Subject.Search(
            new ComplianceDeclarationSearchQuery(),
            3,
            pageSize,
            TestContext.Current.CancellationToken
        );

        page1.ComplianceDeclarations.Should().HaveCount(pageSize);
        page1.Total.Should().Be(5);

        page2.ComplianceDeclarations.Should().HaveCount(pageSize);
        page2.Total.Should().Be(5);

        page3.ComplianceDeclarations.Should().HaveCount(1);
        page3.Total.Should().Be(5);
    }

    [Fact]
    public async Task Search_WhenPageOutOfBounds_ShouldReturnEmptyWithCorrectTotal()
    {
        await Subject.Create(ComplianceDeclarationFixture.Default().Create(), TestContext.Current.CancellationToken);

        var result = await Subject.Search(
            new ComplianceDeclarationSearchQuery(),
            10,
            10,
            TestContext.Current.CancellationToken
        );

        result.ComplianceDeclarations.Should().BeEmpty();
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Search_WhenDeclarationUpdated_ShouldRetainPositionInPagedList()
    {
        const int pageSize = 1;
        var records = new List<ComplianceDeclaration>();

        // Create records in a way that we know their order (by ID)
        // Since we use SortBy(x => x.Id), we need to ensure the ID is the sort key
        for (var i = 0; i < 3; i++)
        {
            records.Add(
                await Subject.Create(
                    ComplianceDeclarationFixture.Default().Create(),
                    TestContext.Current.CancellationToken
                )
            );
        }

        var sortedIds = records.Select(x => x.Id).OrderBy(id => id).ToList();
        var targetRecord = records.First(x => x.Id == sortedIds[1]);

        // Verify initial position (Page 2)
        var search1 = await Subject.Search(
            new ComplianceDeclarationSearchQuery(),
            2,
            pageSize,
            TestContext.Current.CancellationToken
        );
        search1.ComplianceDeclarations.First().Id.Should().Be(targetRecord.Id);

        // Update the record
        var updated = await Subject.Update(
            targetRecord,
            targetRecord with
            {
                ObligationYear = 9999,
            },
            TestContext.Current.CancellationToken
        );

        // Verify position is retained (still Page 2)
        var search2 = await Subject.Search(
            new ComplianceDeclarationSearchQuery(),
            2,
            pageSize,
            TestContext.Current.CancellationToken
        );
        search2.ComplianceDeclarations.First().Id.Should().Be(targetRecord.Id);
        search2.ComplianceDeclarations.First().ObligationYear.Should().Be(9999);
        updated.Updated.Should().BeAfter(targetRecord.Updated);
        search2.ComplianceDeclarations.First().Updated.Should().Be(updated.Updated);
    }

    [Fact]
    public async Task Search_WhenFilteringByOrganisationNameWithRegexCharacters_ShouldTreatLiterally()
    {
        const string regexName = "Waste Management Ltd (UK)";
        const string otherName = "Waste Management Ltd";

        await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, OrganisationFixture.Organisation().With(y => y.Name, regexName).Create())
                .Create(),
            TestContext.Current.CancellationToken
        );
        await Subject.Create(
            ComplianceDeclarationFixture
                .Default()
                .With(x => x.Organisation, OrganisationFixture.Organisation().With(y => y.Name, otherName).Create())
                .Create(),
            TestContext.Current.CancellationToken
        );

        var result = await Subject.Search(
            new ComplianceDeclarationSearchQuery { OrganisationName = regexName },
            1,
            10,
            TestContext.Current.CancellationToken
        );

        result.ComplianceDeclarations.Should().ContainSingle();
        result.ComplianceDeclarations.Should().Contain(x => x.Organisation.Name == regexName);
    }

    private static IEnumerable<object> ToVerifyAuditEvents(IEnumerable<AuditEvent> auditEvents) =>
        auditEvents.Select(x => new
        {
            x.EventId,
            x.Sequence,
            x.Entity,
            x.EntityId,
            x.Operation,
            x.EventType,
            x.DeletedReason,
            x.OccurredAt,
            x.RecordedAt,
            x.Actor,
            x.Version,
            Before = ToPlainDocument(x.Before),
            After = ToPlainDocument(x.After),
            x.SchemaVersion,
        });

    private static HeaderPropagationValues HeaderPropagationValues() =>
        new() { Headers = new Dictionary<string, StringValues> { [TraceHeaderName] = TraceId } };

    private static ComplianceDeclarationService CreateSubject(IMongoDatabase database) =>
        new(
            new MongoDbContext(database),
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System,
            new AuditEventService(new AuditEventDbContext(database), TimeProvider.System, new FakeEventIdGenerator()),
            HeaderPropagationValues(),
            Options.Create(new TraceHeader { Name = TraceHeaderName })
        );

    private static object? ToPlainDocument(BsonDocument? document)
    {
        return document is null ? null : ToPlainValue(document);
    }

    private static object? ToPlainValue(BsonValue value) =>
        value.BsonType switch
        {
            BsonType.Array => value.AsBsonArray.Select(ToPlainValue).ToList(),
            BsonType.Boolean => value.AsBoolean,
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.Decimal128 => value.AsDecimal,
            BsonType.Document => value.AsBsonDocument.ToDictionary(x => x.Name, x => ToPlainValue(x.Value)),
            BsonType.Double => value.AsDouble,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Null => null,
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.String => value.AsString,
            _ => BsonTypeMapper.MapToDotNetValue(value),
        };

    private class ThrowingAuditEventService : IAuditEventService
    {
        public const string Message = "Audit event failed";

        public Task RecordEvent(
            IClientSessionHandle session,
            AuditEventRequest auditEvent,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException(Message);
    }
}
