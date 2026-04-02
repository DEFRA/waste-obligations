using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligationsRequest
{
    [Description("Year of obligation(s)")]
    [FromQuery(Name = "year")]
    [Range(2000, int.MaxValue, ErrorMessage = "Year must be 2000 onwards")]
    public int? Year { get; init; }
}
