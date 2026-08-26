namespace Defra.WasteObligations.Api.Services;

public record ComplianceDeclarationReviewStateBackfillResult
{
    public required bool AlreadyComplete { get; init; }
    public required int StateRowCount { get; init; }
}
