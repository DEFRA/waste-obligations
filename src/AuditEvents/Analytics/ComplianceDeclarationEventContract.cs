namespace Defra.WasteObligations.AuditEvents.Analytics;

public static class ComplianceDeclarationEventContract
{
    public const string Entity = "compliance_declaration";
    public const string CreatedEventType = "submission.created";
    public const string UpdatedEventType = "submission.amended";
    public const string RemovedEventType = "submission.removed";
    public const string IdentityPrefix = "cdec_";
}
