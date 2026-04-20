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
        retrieved.Should().BeEquivalentTo(initial, options => options.AllowMongoDateTimePrecision());
    }
}
