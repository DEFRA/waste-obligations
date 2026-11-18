namespace Defra.WasteObligations.AuditEvents.Analytics;

public static class AnalyticsEventVocabulary
{
    public const string ElevatedSystemAllowedRemoval = "elevated_system_allowed_removal";

    private const string CreateOperation = "create";
    private const string UpdateOperation = "update";
    private const string DeleteOperation = "delete";
    private const string LegacyElevatedSystemAllowedRemoval = "elevated system allowed removal";

    private static readonly Dictionary<string, EntityRegistration> s_entities = new()
    {
        ["compliance_declaration"] = new(
            "cdec_",
            [CreateOperation, UpdateOperation, DeleteOperation],
            ["submission.created", "submission.amended", "submission.removed"],
            [
                "compliance_declaration_v1.0",
                "compliance_declaration_v1.1",
                "compliance_declaration_v1.2",
                "compliance_declaration_v1.3",
            ]
        ),
        ["obligation_calculation"] = new(
            "oblcalc_",
            [CreateOperation, UpdateOperation],
            ["obligation_calculation.created", "obligation_calculation.updated"],
            ["obligation_calculation_v1.0"]
        ),
    };

    private static readonly HashSet<string> s_actorPrefixes = ["service", "user", "system", "integration"];

    public static string ToOperation(string operation) =>
        operation switch
        {
            "insert" => CreateOperation,
            UpdateOperation => UpdateOperation,
            DeleteOperation => DeleteOperation,
            _ => throw new InvalidOperationException($"Unregistered analytics operation '{operation}'."),
        };

    public static string ToEntityId(string entity, string entityId) => GetEntity(entity).EntityIdPrefix + entityId;

    public static string? NormalizeDeletedReason(string? deletedReason) =>
        deletedReason switch
        {
            LegacyElevatedSystemAllowedRemoval => ElevatedSystemAllowedRemoval,
            _ => deletedReason,
        };

    public static void Validate(AnalyticsEvent analyticsEvent)
    {
        var entity = GetEntity(analyticsEvent.Entity);

        if (!entity.Operations.Contains(analyticsEvent.Operation))
            throw new InvalidOperationException($"Unregistered analytics operation '{analyticsEvent.Operation}'.");

        if (!entity.EventTypes.Contains(analyticsEvent.EventType))
            throw new InvalidOperationException($"Unregistered analytics event type '{analyticsEvent.EventType}'.");

        ValidateActor(analyticsEvent.Actor);

        if (!entity.SchemaVersions.Contains(analyticsEvent.SchemaVersion))
            throw new InvalidOperationException(
                $"Unregistered analytics schema version '{analyticsEvent.SchemaVersion}'."
            );

        if (analyticsEvent.Operation == DeleteOperation)
        {
            if (analyticsEvent.DeletedReason != ElevatedSystemAllowedRemoval)
                throw new InvalidOperationException(
                    $"Unregistered analytics deletion reason '{analyticsEvent.DeletedReason}'."
                );

            return;
        }

        if (analyticsEvent.DeletedReason is not null)
            throw new InvalidOperationException("An analytics deletion reason is only valid for delete operations.");
    }

    private static EntityRegistration GetEntity(string entity) =>
        s_entities.TryGetValue(entity, out var registration)
            ? registration
            : throw new InvalidOperationException($"Unregistered analytics entity '{entity}'.");

    private static void ValidateActor(string actor)
    {
        var separatorIndex = actor.IndexOf(':');
        if (
            separatorIndex <= 0
            || separatorIndex == actor.Length - 1
            || !s_actorPrefixes.Contains(actor[..separatorIndex])
        )
            throw new InvalidOperationException($"Unregistered analytics actor '{actor}'.");
    }

    private sealed record EntityRegistration(
        string EntityIdPrefix,
        HashSet<string> Operations,
        HashSet<string> EventTypes,
        HashSet<string> SchemaVersions
    );
}
