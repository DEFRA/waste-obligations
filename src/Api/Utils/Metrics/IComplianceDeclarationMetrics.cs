using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Utils.Metrics;

public interface IComplianceDeclarationMetrics
{
    void Created();

    void Updated(ComplianceDeclarationStatus status);

    void Deleted();
}
