using System.Text.Json.Serialization;

namespace Defra.WasteObligations.Api.Dtos;

public record ComplianceDeclarationsPaged
{
    [JsonPropertyName("complianceDeclarations")]
    public IEnumerable<ComplianceDeclaration> ComplianceDeclarations { get; init; } = [];

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }
}
