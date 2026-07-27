using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Dtos.Attributes;
using Defra.WasteObligations.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record SearchOrganisationPrnsRequest
{
    [Description("Searches PRN number or issuer organisation name")]
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [Description("PRN status")]
    [FromQuery(Name = "status")]
    [EnumValue<OrganisationPrnStatus>(ErrorMessage = "Invalid PRN status")]
    public string? Status { get; init; }

    [Description("PRN list sort order")]
    [FromQuery(Name = "sort")]
    [EnumValue<OrganisationPrnSort>(ErrorMessage = "Invalid PRN sort")]
    public string? Sort { get; init; }

    [Description("Page number (1-based), defaults to 1 if not specified")]
    [FromQuery(Name = "page")]
    [Minimum(1)]
    public int? Page { get; init; }

    [Description("Number of items per page, defaults to 20 if not specified, max of 100")]
    [FromQuery(Name = "pageSize")]
    [Range(1, 100)]
    public int? PageSize { get; init; }

    public int EffectivePage => Page ?? 1;
    public int EffectivePageSize => PageSize ?? 20;

    public OrganisationPrnStatus? ParsedStatus() => Status?.FromJsonValue<OrganisationPrnStatus>();

    public OrganisationPrnSort? ParsedSort() => Sort?.FromJsonValue<OrganisationPrnSort>();
}
