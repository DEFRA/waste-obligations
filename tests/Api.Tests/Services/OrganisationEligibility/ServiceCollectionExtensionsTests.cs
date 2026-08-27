using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOrganisationEligibility_ShouldRegisterTheWorkersByDefault()
    {
        var services = new ServiceCollection();

        services.AddOrganisationEligibility();

        services
            .Should()
            .Contain(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(OrganisationEligibilityRefreshWorker)
            );
        services
            .Should()
            .Contain(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType
                    == typeof(ComplianceDeclarationReviewStateInitialRolloutReconciliationWorker)
            );
    }
}
