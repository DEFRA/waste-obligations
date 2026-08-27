namespace Defra.WasteObligations.Api.Services;

public interface IComplianceDeclarationReviewStateBackfillService
{
    Task<ComplianceDeclarationReviewStateBackfillResult> Backfill(CancellationToken cancellationToken);

    // TEMPORARY INITIAL ROLLOUT: Remove after InitialRolloutReconciliationCompletedAt is populated everywhere.
    Task<ComplianceDeclarationReviewStateBackfillResult> ReconcileInitialRollout(CancellationToken cancellationToken);
}
