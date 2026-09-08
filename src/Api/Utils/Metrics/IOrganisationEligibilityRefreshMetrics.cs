using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;

namespace Defra.WasteObligations.Api.Utils.Metrics;

public interface IOrganisationEligibilityRefreshMetrics
{
    void Completed(OrganisationEligibilityRefreshResult result, TimeSpan duration);

    void Failed(TimeSpan duration);

    void ReferenceResolutionObserved(IReadOnlyCollection<OrganisationComplianceDeclarationEligibility> rows);

    void WasteOrganisationsReadCompleted(int organisationCount, TimeSpan duration);

    void WasteOrganisationsReadFailed(TimeSpan duration);

    void AccountReferenceLookupCompleted(RegistrationType registrationType, int lookupKeyCount, TimeSpan duration);

    void AccountReferenceLookupFailed(RegistrationType registrationType, TimeSpan duration);
}
