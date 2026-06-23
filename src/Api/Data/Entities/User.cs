namespace Defra.WasteObligations.Api.Data.Entities;

public record User
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string Name { get; init; }
}
