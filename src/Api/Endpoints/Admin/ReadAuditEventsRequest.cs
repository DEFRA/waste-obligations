using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Defra.WasteObligations.Api.Endpoints.Admin;

public record ReadAuditEventsRequest
{
    public const int DefaultLimit = 100;
    public const int MaximumLimit = 500;

    [FromQuery(Name = "afterSequence")]
    [Range(0, long.MaxValue)]
    public long? AfterSequence { get; init; }

    [FromQuery(Name = "limit")]
    [Range(1, MaximumLimit)]
    public int? Limit { get; init; }

    [FromQuery(Name = "entity")]
    [StringLength(100)]
    public string? Entity { get; init; }

    [FromQuery(Name = "entityId")]
    [StringLength(100)]
    public string? EntityId { get; init; }

    public int EffectiveLimit => Limit ?? DefaultLimit;
}
