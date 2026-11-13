using System.Text.Json;

namespace Defra.WasteObligations.Api.Services.Packaging;

public record PackagingResponse
{
    // Map each group during processing so invalid submitter data does not reject other groups.
    public required IReadOnlyList<JsonElement> Items { get; init; }
}
