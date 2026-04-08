using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;
using Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Services;

public class OrganisationServiceTests
{
    [Fact]
    public async Task ReadOrganisation_OrganisationAlwaysFound()
    {
        var subject = new OrganisationService(Substitute.For<IPrnCommonBackendService>());

        var result = await subject.ReadOrganisation(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadObligations_WhenNotFound_ShouldBeEmpty()
    {
        var subject = new OrganisationService(Substitute.For<IPrnCommonBackendService>());

        var result = await subject.ReadObligations(Guid.NewGuid(), 2026, TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadObligations_WhenFound_ShouldNotBeEmpty()
    {
        var organisationId = Guid.NewGuid();
        const int year = 2026;
        var prnCommonBackendService = Substitute.For<IPrnCommonBackendService>();
        prnCommonBackendService
            .ReadObligations(organisationId, year, TestContext.Current.CancellationToken)
            .Returns(ObligationsFixture.Default().Create());
        var subject = new OrganisationService(prnCommonBackendService);

        var result = await subject.ReadObligations(organisationId, year, TestContext.Current.CancellationToken);

        result.Should().NotBeEmpty();
    }
}
