using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteObligations.Api.Utils.Metrics;

[ExcludeFromCodeCoverage]
public static class Metrics
{
    public const string MeterName = "Defra.WasteObligationsApi";

    public static class Names
    {
        public const string ComplianceDeclarationCreated = nameof(ComplianceDeclarationCreated);
        public const string ComplianceDeclarationUpdated = nameof(ComplianceDeclarationUpdated);
        public const string ComplianceDeclarationDeleted = nameof(ComplianceDeclarationDeleted);
        public const string AuditEventSnsPublish = nameof(AuditEventSnsPublish);
        public const string AuditEventSnsPublishActive = nameof(AuditEventSnsPublishActive);
        public const string AuditEventSnsPublishErrors = nameof(AuditEventSnsPublishErrors);
        public const string AuditEventSnsPublishDuration = nameof(AuditEventSnsPublishDuration);
        public const string AuditEventSnsPublishLatency = nameof(AuditEventSnsPublishLatency);
    }

    public static class Tags
    {
        public const string Service = nameof(Service);
        public const string HttpMethod = nameof(HttpMethod);
        public const string RequestPath = nameof(RequestPath);
        public const string StatusCode = nameof(StatusCode);
        public const string ExceptionType = nameof(ExceptionType);
        public const string ComplianceDeclarationStatus = nameof(ComplianceDeclarationStatus);
        public const string ProcessName = nameof(ProcessName);
        public const string TopicName = nameof(TopicName);
        public const string Entity = nameof(Entity);
        public const string Operation = nameof(Operation);
        public const string EventType = nameof(EventType);
    }
}
