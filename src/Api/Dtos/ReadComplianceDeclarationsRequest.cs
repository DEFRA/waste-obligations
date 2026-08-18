using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Dtos.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record ReadComplianceDeclarationsRequest
{
    [FromQuery(Name = "obligationYear")]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    public int? ObligationYear { get; init; }

    public int ObligationYearValue => ObligationYear.GetValueOrDefault();

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
}
