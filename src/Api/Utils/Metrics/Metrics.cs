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
        public const string EmailSend = nameof(EmailSend);
        public const string EmailSendActive = nameof(EmailSendActive);
        public const string EmailSendErrors = nameof(EmailSendErrors);
        public const string EmailSendDuration = nameof(EmailSendDuration);
        public const string AuditEventDispatchPoll = nameof(AuditEventDispatchPoll);
        public const string AuditEventDispatchPollActive = nameof(AuditEventDispatchPollActive);
        public const string AuditEventDispatchPollErrors = nameof(AuditEventDispatchPollErrors);
        public const string AuditEventDispatchPollDuration = nameof(AuditEventDispatchPollDuration);
        public const string AuditEventDispatchRead = nameof(AuditEventDispatchRead);
        public const string AuditEventDispatchBatchSize = nameof(AuditEventDispatchBatchSize);
        public const string AuditEventDispatchLag = nameof(AuditEventDispatchLag);
        public const string AuditEventDispatchOutcome = nameof(AuditEventDispatchOutcome);
        public const string AuditEventDispatchMarkFailures = nameof(AuditEventDispatchMarkFailures);
        public const string AuditEventDispatchLease = nameof(AuditEventDispatchLease);
        public const string AuditEventSnsPublish = nameof(AuditEventSnsPublish);
        public const string AuditEventSnsPublishActive = nameof(AuditEventSnsPublishActive);
        public const string AuditEventSnsPublishErrors = nameof(AuditEventSnsPublishErrors);
        public const string AuditEventSnsPublishDuration = nameof(AuditEventSnsPublishDuration);
        public const string AuditEventSnsPublishLatency = nameof(AuditEventSnsPublishLatency);
        public const string OrganisationObligationHydrationFailure = nameof(OrganisationObligationHydrationFailure);
        public const string OrganisationObligationHydrationLeaseNotAcquired = nameof(
            OrganisationObligationHydrationLeaseNotAcquired
        );
        public const string OrganisationObligationHydrationSuccess = nameof(OrganisationObligationHydrationSuccess);
        public const string OrganisationObligationHydrationObligationReadDuration = nameof(
            OrganisationObligationHydrationObligationReadDuration
        );
        public const string OrganisationObligationHydrationObligationReadFailure = nameof(
            OrganisationObligationHydrationObligationReadFailure
        );
        public const string OrganisationObligationHydrationActiveSummaryCount = nameof(
            OrganisationObligationHydrationActiveSummaryCount
        );
        public const string OrganisationObligationHydrationDueSummaryCount = nameof(
            OrganisationObligationHydrationDueSummaryCount
        );
        public const string OrganisationObligationHydrationStaleSummaryAge = nameof(
            OrganisationObligationHydrationStaleSummaryAge
        );
        public const string OrganisationObligationHydrationStaleSummaryCount = nameof(
            OrganisationObligationHydrationStaleSummaryCount
        );
        public const string OrganisationEligibilityRefreshDuration = nameof(OrganisationEligibilityRefreshDuration);
        public const string OrganisationEligibilityRefreshLeaseNotAcquired = nameof(
            OrganisationEligibilityRefreshLeaseNotAcquired
        );
        public const string OrganisationEligibilityRefreshOutcome = nameof(OrganisationEligibilityRefreshOutcome);
        public const string OrganisationEligibilityRefreshRowCount = nameof(OrganisationEligibilityRefreshRowCount);
        public const string OrganisationEligibilityReferenceResolutionCount = nameof(
            OrganisationEligibilityReferenceResolutionCount
        );
        public const string OrganisationEligibilityRefreshWasteOrganisationsReadDuration = nameof(
            OrganisationEligibilityRefreshWasteOrganisationsReadDuration
        );
        public const string OrganisationEligibilityRefreshWasteOrganisationsReadFailure = nameof(
            OrganisationEligibilityRefreshWasteOrganisationsReadFailure
        );
        public const string OrganisationEligibilityRefreshWasteOrganisationsRowCount = nameof(
            OrganisationEligibilityRefreshWasteOrganisationsRowCount
        );
        public const string OrganisationEligibilityRefreshAccountReferenceLookupDuration = nameof(
            OrganisationEligibilityRefreshAccountReferenceLookupDuration
        );
        public const string OrganisationEligibilityRefreshAccountReferenceLookupFailure = nameof(
            OrganisationEligibilityRefreshAccountReferenceLookupFailure
        );
        public const string OrganisationEligibilityRefreshAccountReferenceLookupBatchSize = nameof(
            OrganisationEligibilityRefreshAccountReferenceLookupBatchSize
        );
        public const string OrganisationObligationHydrationMinimumFullRefreshDuration = nameof(
            OrganisationObligationHydrationMinimumFullRefreshDuration
        );
        public const string OrganisationObligationHydrationRefreshInterval = nameof(
            OrganisationObligationHydrationRefreshInterval
        );
        public const string OrganisationObligationHydrationMaxDownstreamRequestsPerMinute = nameof(
            OrganisationObligationHydrationMaxDownstreamRequestsPerMinute
        );
    }

    public static class Tags
    {
        public const string Service = nameof(Service);
        public const string HttpMethod = nameof(HttpMethod);
        public const string RequestPath = nameof(RequestPath);
        public const string StatusCode = nameof(StatusCode);
        public const string ExceptionType = nameof(ExceptionType);
        public const string ComplianceDeclarationStatus = nameof(ComplianceDeclarationStatus);
        public const string TemplateName = nameof(TemplateName);
        public const string Language = nameof(Language);
        public const string ProcessName = nameof(ProcessName);
        public const string TopicName = nameof(TopicName);
        public const string DispatchStatus = nameof(DispatchStatus);
        public const string DispatchOutcome = nameof(DispatchOutcome);
        public const string LeaseOutcome = nameof(LeaseOutcome);
        public const string Entity = nameof(Entity);
        public const string Operation = nameof(Operation);
        public const string EventType = nameof(EventType);
        public const string Outcome = nameof(Outcome);
        public const string ReferenceResolutionState = nameof(ReferenceResolutionState);
        public const string AccountReferenceLookupType = nameof(AccountReferenceLookupType);
    }
}
