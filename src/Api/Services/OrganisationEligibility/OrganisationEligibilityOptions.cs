using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public record OrganisationEligibilityOptions
{
    public const string SectionName = "OrganisationEligibility";

    [Range(1, 1000)]
    public int AccountReferenceNumberBatchSize { get; init; } = 100;

    public TimeSpan ReferenceNumberRetryDelay { get; init; } = TimeSpan.FromHours(6);
}
