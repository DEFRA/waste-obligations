using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Dtos = Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public record ReadFailedOrganisationObligationSummariesRequest
{
    [FromQuery(Name = "obligationYear")]
    [Required]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    public int? ObligationYear { get; init; }
}
