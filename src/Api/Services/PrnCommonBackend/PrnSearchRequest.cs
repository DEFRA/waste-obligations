namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record PrnSearchRequest
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public string? Search { get; init; }
    public string? FilterBy { get; init; }
    public required string SortBy { get; init; }
}
