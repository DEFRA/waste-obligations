namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public record PrnSearchRequest
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public string? Search { get; init; }
    public string? FilterBy { get; init; }
    public string? SortBy { get; init; }
}
