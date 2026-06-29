using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.AuditEvents.Analytics;

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class AnalyticsEventFixture
{
    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<AnalyticsEvent> AddDefaults(this ICustomizationComposer<AnalyticsEvent> composer)
    {
        return composer
            .With(x => x.Entity, "entity")
            .With(x => x.EntityId, "entity-1")
            .With(x => x.Operation, "insert")
            .With(x => x.OccurredAt, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .With(x => x.RecordedAt, new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero))
            .With(x => x.Actor, "user@example.com")
            .With(x => x.Version, 1)
            .With(x => x.SchemaVersion, "entity.v1.0")
            .Without(x => x.Before)
            .Without(x => x.After);
    }

    public static IPostprocessComposer<AnalyticsEvent> Event()
    {
        return GetFixture().Build<AnalyticsEvent>().AddDefaults();
    }

    public static IPostprocessComposer<AnalyticsEvent> Default(string eventId = "event-1", long sequence = 1)
    {
        return Event().With(x => x.EventId, eventId).With(x => x.Sequence, sequence);
    }

    public static IPostprocessComposer<AnalyticsEvent> ComplianceDeclaration(
        string eventId = "event-1",
        long sequence = 1
    )
    {
        return Default(eventId, sequence)
            .With(x => x.Entity, "compliance_declaration")
            .With(x => x.EntityId, "compliance_declaration_entity-1")
            .With(
                x => x.SchemaVersion,
                $"compliance_declaration.{Api.Data.Entities.ComplianceDeclaration.SchemaVersionValue}"
            );
    }
}
