using AutoFixture;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class AuditEntryFixture
{
    public static IEnumerable<AuditEntry> Submitted(DateTime? timestamp = null) =>
        [
            new("Submitted")
            {
                User = UserFixture.Default().Create(),
                Timestamp = timestamp ?? new DateTime(2026, 4, 26, 14, 0, 0, DateTimeKind.Utc),
            },
        ];

    public static IEnumerable<AuditEntry> SubmittedThenCancelled(DateTime? timestamp = null) =>
        [
            new("Submitted")
            {
                User = UserFixture.Default().Create(),
                Timestamp = timestamp ?? new DateTime(2026, 4, 26, 14, 0, 0, DateTimeKind.Utc),
            },
            new ReasonAuditEntry("Cancelled")
            {
                Reason = "Invalid",
                User = UserFixture.Default().Create(),
                Timestamp = timestamp ?? new DateTime(2026, 4, 26, 14, 10, 0, DateTimeKind.Utc),
            },
        ];
}
