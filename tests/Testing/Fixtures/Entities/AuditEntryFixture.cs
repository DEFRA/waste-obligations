using Defra.WasteObligations.Api.Data.Entities;
using UserLocale = Defra.WasteObligations.Api.Dtos.UserLocale;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class AuditEntryFixture
{
    private static User SubmitterUser(string? locale = UserLocale.En) =>
        new()
        {
            Id = "e72be574-8b5b-4836-af47-dd7e0c0d1d87",
            Email = "submitter@email.com",
            Name = "Submitter Name",
            Locale = locale,
        };

    public static IEnumerable<AuditEntry> Submitted(DateTime? timestamp = null, string? locale = UserLocale.En) =>
        [
            new(nameof(ComplianceDeclarationStatus.Submitted))
            {
                User = SubmitterUser(locale),
                Timestamp = timestamp ?? new DateTime(2026, 4, 26, 14, 0, 0, DateTimeKind.Utc),
            },
        ];

    public static IEnumerable<AuditEntry> Cancelled(DateTime? timestamp = null) =>
        [
            new ReasonAuditEntry(nameof(ComplianceDeclarationStatus.Cancelled))
            {
                Reason = "Invalid",
                User = SubmitterUser(),
                Timestamp = timestamp ?? new DateTime(2026, 4, 26, 14, 10, 0, DateTimeKind.Utc),
            },
        ];

    public static IEnumerable<AuditEntry> SubmittedThenCancelled(
        DateTime? timestamp = null,
        string? locale = UserLocale.En
    ) => Submitted(timestamp, locale).Concat(Cancelled(timestamp));
}
