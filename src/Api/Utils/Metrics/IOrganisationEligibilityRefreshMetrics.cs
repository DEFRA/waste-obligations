using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;

namespace Defra.WasteObligations.Api.Utils.Metrics;

public interface IOrganisationEligibilityRefreshMetrics
{
    void Completed(OrganisationEligibilityRefreshResult result, TimeSpan duration);

    void Failed(TimeSpan duration);

    void LeaseSkipped();

    void ReferenceResolutionObserved(IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> rows);
}
