using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record OrganisationObligationsRequest
{
    [Description("Year of obligation(s)")]
    [FromQuery(Name = "year")]
    public int? Year { get; init; }
}
