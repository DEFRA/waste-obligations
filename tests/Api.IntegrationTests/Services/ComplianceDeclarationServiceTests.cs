using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class ComplianceDeclarationServiceTests : IntegrationTestBase
{
    private ComplianceDeclarationService Subject { get; } =
        new(
            new MongoDbContext(GetMongoDatabase()),
            Substitute.For<ILogger<ComplianceDeclarationService>>(),
            TimeProvider.System
        );

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

        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(initial);
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
        retrieved = retrieved with { ObligationYear = 2027 };

        retrieved = await Subject.Update(retrieved, TestContext.Current.CancellationToken);
        retrieved.Version.Should().Be(2);
        retrieved.Updated.Should().BeAfter(retrieved.Created);

        retrieved.ObligationYear.Should().Be(2027);

        retrieved = await Subject.Read(initial.Id.ToString(), TestContext.Current.CancellationToken);

        retrieved.Should().NotBeNull();
        retrieved.ObligationYear.Should().Be(2027);
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

        retrieved1 = retrieved1 with { ObligationYear = 2027 };
        await Subject.Update(retrieved1, TestContext.Current.CancellationToken);

        retrieved2 = retrieved2 with { ObligationYear = 2028 };
        var act = async () => await Subject.Update(retrieved2, TestContext.Current.CancellationToken);

        await act.Should()
            .ThrowAsync<ConcurrencyException>()
            .WithMessage($"Concurrency issue on write, compliance declaration with id '{initial.Id}' was not updated");
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

        var result = await Subject.Search(targetYear, null, null, 1, 10, TestContext.Current.CancellationToken);

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
            null,
            [ComplianceDeclarationStatus.Submitted, ComplianceDeclarationStatus.Cancelled],
            null,
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
            null,
            null,
            name.ToLower(),
            1,
            10,
            TestContext.Current.CancellationToken
        );
        var resultUppercase = await Subject.Search(
            null,
            null,
            name.ToUpper(),
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

        var page1 = await Subject.Search(null, null, null, 1, pageSize, TestContext.Current.CancellationToken);
        var page2 = await Subject.Search(null, null, null, 2, pageSize, TestContext.Current.CancellationToken);
        var page3 = await Subject.Search(null, null, null, 3, pageSize, TestContext.Current.CancellationToken);

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

        var result = await Subject.Search(null, null, null, 10, 10, TestContext.Current.CancellationToken);

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
        var search1 = await Subject.Search(null, null, null, 2, pageSize, TestContext.Current.CancellationToken);
        search1.ComplianceDeclarations.First().Id.Should().Be(targetRecord.Id);

        // Update the record
        var updated = await Subject.Update(
            targetRecord with
            {
                ObligationYear = 9999,
            },
            TestContext.Current.CancellationToken
        );

        // Verify position is retained (still Page 2)
        var search2 = await Subject.Search(null, null, null, 2, pageSize, TestContext.Current.CancellationToken);
        search2.ComplianceDeclarations.First().Id.Should().Be(targetRecord.Id);
        search2.ComplianceDeclarations.First().ObligationYear.Should().Be(9999);
        updated.Updated.Should().BeAfter(targetRecord.Updated);
        search2.ComplianceDeclarations.First().Updated.Should().Be(updated.Updated);
    }
}
