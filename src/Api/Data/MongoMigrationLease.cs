namespace Defra.WasteObligations.Api.Data;

public record MongoMigrationLease
{
    public required string Id { get; init; }

    public required string Owner { get; init; }

    public required DateTime ExpiresAt { get; init; }
}
