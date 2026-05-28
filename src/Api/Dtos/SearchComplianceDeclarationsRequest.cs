using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Defra.WasteObligations.Api.Dtos.Attributes;
using Defra.WasteObligations.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Dtos;

public record SearchComplianceDeclarationsRequest
{
    [FromQuery(Name = "obligationYear")]
    [Range(Dtos.ObligationYear.Minimum, Dtos.ObligationYear.Maximum)]
    public int? ObligationYear { get; init; }

    [Description("Comma separated list of compliance declaration status")]
    [FromQuery(Name = "status")]
    [EnumCommaSeparatedList<ComplianceDeclarationStatus>(ErrorMessage = "Invalid compliance declaration status(s)")]
    public string? Status { get; init; }

    [FromQuery(Name = "organisationName")]
    public string? OrganisationName { get; init; }

    [Description("Page number (1-based), defaults to 1 if not specified")]
    [Range(1, int.MaxValue)]
    [FromQuery(Name = "page")]
    public int? Page { get; init; } = 1;

    [Description("Number of items per page, defaults to 20 if not specified, max of 100")]
    [Range(1, 100)]
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; } = 20;

    public List<ComplianceDeclarationStatus> ParsedStatus() =>
        Status?.Split(',').NotNull().Select(x => x.FromJsonValue<ComplianceDeclarationStatus>()).ToList() ?? [];
}
