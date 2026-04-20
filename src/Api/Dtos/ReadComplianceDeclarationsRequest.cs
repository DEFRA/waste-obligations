using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record ReadComplianceDeclarationsRequest
{
    [FromQuery(Name = "obligationYear")]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    public int? ObligationYear { get; init; }

    public int ObligationYearValue => ObligationYear.GetValueOrDefault();
}
