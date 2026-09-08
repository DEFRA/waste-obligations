namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedReferenceResolutionIssues
{
    public string? ActiveGeneration { get; init; }

    public required UnsubmittedReferenceResolutionIssue[] ReferenceResolutionIssues { get; init; }
}
