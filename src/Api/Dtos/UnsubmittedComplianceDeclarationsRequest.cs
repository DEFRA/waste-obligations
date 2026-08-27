using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Dtos.Attributes;
using Defra.WasteObligations.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record UnsubmittedComplianceDeclarationsRequest
{
    private const int SearchMaxLength = 100;

    [Required]
    [FromQuery(Name = "obligationYear")]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    public int? ObligationYear { get; init; }

    [Required]
    [FromQuery(Name = "registrationType")]
    [EnumValue<RegistrationType>(ErrorMessage = "Invalid organisation registration type")]
    public string? RegistrationType { get; init; }

    [Description("Case-insensitive partial match on organisation name, trading name or reference number")]
    [StringLength(SearchMaxLength)]
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [Description("Only OrganisationName[asc] or OrganisationName[desc] is currently supported")]
    [FromQuery(Name = "sort")]
    [ComplianceDeclarationSortList(ErrorMessage = "Invalid unsubmitted compliance declaration sort")]
    public string? Sort { get; init; }

    [Description("Page number (1-based), defaults to 1 if not specified")]
    [Minimum(Paging.MinimumPage)]
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    [Description("Number of items per page, defaults to 20 if not specified, max of 100")]
    [Range(Paging.MinimumPageSize, Paging.MaximumPageSize)]
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    public int EffectivePage => Page ?? Paging.DefaultPage;
    public int EffectivePageSize => PageSize ?? Paging.DefaultPageSize;

    public Data.Entities.RegistrationType ParsedRegistrationType() =>
        RegistrationType!.FromJsonValue<RegistrationType>().ToEntity();

    public Data.ComplianceDeclarationSort[] ParsedSort() => ComplianceDeclarationSortParser.Parse(Sort);
}
