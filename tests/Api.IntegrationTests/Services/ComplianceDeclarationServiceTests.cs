using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
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
}
