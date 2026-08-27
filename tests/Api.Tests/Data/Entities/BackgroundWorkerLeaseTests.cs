using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Defra.WasteObligations.Api.Tests.Data.Entities;

public class BackgroundWorkerLeaseTests
{
    [Fact]
    public void Serialize_ShouldUseExistingLeaseDocumentShape()
    {
        var expiresAt = new DateTime(2026, 8, 27, 12, 30, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 8, 27, 12, 15, 0, DateTimeKind.Utc);
        var lastReleasedAt = new DateTime(2026, 8, 27, 12, 20, 0, DateTimeKind.Utc);
        var lease = new BackgroundWorkerLease
        {
            Id = BackgroundWorkerLease.OrganisationObligationHydrationLeaseId,
            Owner = "host-1",
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            LastReleasedAt = lastReleasedAt,
        };
        var existingLeaseDocument = new BsonDocument
        {
            ["_id"] = lease.Id,
            ["owner"] = lease.Owner,
            ["expiresAt"] = expiresAt,
            ["createdAt"] = createdAt,
            ["updatedAt"] = updatedAt,
            ["lastReleasedAt"] = lastReleasedAt,
        };

        lease.ToBsonDocument().Elements.Should().Equal(existingLeaseDocument.Elements);

        BsonSerializer.Deserialize<BackgroundWorkerLease>(existingLeaseDocument).Should().Be(lease);
    }
}
