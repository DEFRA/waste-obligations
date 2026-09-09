using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos.Attributes;
using Defra.WasteObligations.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public record ReadOrganisationEligibilityRequest
{
    [FromQuery(Name = "generation")]
    public string? Generation { get; init; }

    [FromQuery(Name = "referenceResolutionState")]
    [EnumValue<OrganisationReferenceNumberResolutionState>(ErrorMessage = "Invalid reference resolution state")]
    public string? ReferenceResolutionState { get; init; }

    public OrganisationReferenceNumberResolutionState? ParsedReferenceResolutionState() =>
        ReferenceResolutionState is null
            ? null
            : ReferenceResolutionState.FromJsonValue<OrganisationReferenceNumberResolutionState>();
}
