namespace Defra.WasteObligations.Api.Services;

public interface IComplianceDeclarationReviewStateBackfillService
{
    Task<ComplianceDeclarationReviewStateBackfillResult> Backfill(CancellationToken cancellationToken);
}
