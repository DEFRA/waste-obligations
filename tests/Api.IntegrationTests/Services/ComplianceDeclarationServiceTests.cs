using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing.Extensions;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.WasteObligations.Api.IntegrationTests.Services;

public class ComplianceDeclarationServiceTests : IntegrationTestBase
{
    private ComplianceDeclarationService Subject { get; } =
        new(new MongoDbContext(GetMongoDatabase()), Substitute.For<ILogger<ComplianceDeclarationService>>());

    [Fact]
    public async Task Read_WhenNoComplianceDeclaration_ShouldBeNull()
    {
        var complianceDeclaration = await Subject.Read(Guid.NewGuid(), TestContext.Current.CancellationToken);

        complianceDeclaration.Should().BeNull();
    }

    [Fact]
    public async Task Create_WhenInserted_ShouldBeFound()
    {
        var initial = await Subject.Create(
            ComplianceDeclarationFixture.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var retrieved = await Subject.Read(initial.Id, TestContext.Current.CancellationToken);

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
}
