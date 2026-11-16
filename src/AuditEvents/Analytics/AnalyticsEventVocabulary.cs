namespace Defra.WasteObligations.AuditEvents.Analytics;

public static class AnalyticsEventVocabulary
{
    public const string ElevatedSystemAllowedRemoval = "elevated_system_allowed_removal";

    private const string CreateOperation = "create";
    private const string UpdateOperation = "update";
    private const string DeleteOperation = "delete";
    private const string LegacyElevatedSystemAllowedRemoval = "elevated system allowed removal";

    private static readonly HashSet<string> s_declarationEventTypes =
    [
        ComplianceDeclarationEventContract.CreatedEventType,
        ComplianceDeclarationEventContract.UpdatedEventType,
        ComplianceDeclarationEventContract.RemovedEventType,
    ];

    private static readonly Dictionary<string, AnalyticsEntityRegistration> s_entities = new()
    {
        [ComplianceDeclarationEventContract.Entity] = new(
            ComplianceDeclarationEventContract.IdentityPrefix,
            new Dictionary<string, HashSet<string>>
            {
                [CreateOperation] = s_declarationEventTypes,
                [UpdateOperation] = s_declarationEventTypes,
                [DeleteOperation] = s_declarationEventTypes,
            }
        ),
        [ObligationCalculationEventContract.Entity] = new(
            ObligationCalculationEventContract.IdentityPrefix,
            new Dictionary<string, HashSet<string>>
            {
                [CreateOperation] = [ObligationCalculationEventContract.CreatedEventType],
                [UpdateOperation] = [ObligationCalculationEventContract.UpdatedEventType],
            }
        ),
    };

    private static readonly HashSet<string> s_actorPrefixes = ["service", "user", "system", "integration"];

    internal static string GetLegacyEntityId(string entity, string entityId) =>
        $"{GetEntity(entity).LegacyIdentityPrefix}{entityId}";

    private static AnalyticsEntityRegistration GetEntity(string entity) =>
        s_entities.TryGetValue(entity, out var registration)
            ? registration
            : throw new InvalidOperationException($"Unregistered analytics entity '{entity}'.");

    public static string ToOperation(string operation) =>
        operation switch
        {
            "insert" => CreateOperation,
            UpdateOperation => UpdateOperation,
            DeleteOperation => DeleteOperation,
            _ => throw new InvalidOperationException($"Unregistered analytics operation '{operation}'."),
        };

    public static string? NormalizeDeletedReason(string? deletedReason) =>
        deletedReason switch
        {
            LegacyElevatedSystemAllowedRemoval => ElevatedSystemAllowedRemoval,
            _ => deletedReason,
        };

    public static void Validate(AnalyticsEvent analyticsEvent)
    {
        var entity = GetEntity(analyticsEvent.Entity);
        if (analyticsEvent.Operation is not (CreateOperation or UpdateOperation or DeleteOperation))
            throw new InvalidOperationException($"Unregistered analytics operation '{analyticsEvent.Operation}'.");

        if (
            !entity.EventTypesByOperation.TryGetValue(analyticsEvent.Operation, out var eventTypes)
            || !eventTypes.Contains(analyticsEvent.EventType)
        )
            throw new InvalidOperationException($"Unregistered analytics event type '{analyticsEvent.EventType}'.");

        ValidateActor(analyticsEvent.Actor);

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
}
