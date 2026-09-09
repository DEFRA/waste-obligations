using System.Diagnostics.CodeAnalysis;

// ReSharper disable MemberHidesStaticFromOuterClass

namespace Defra.WasteObligations.Testing;

[SuppressMessage(
    "Critical Code Smell",
    "S3218:Inner class members should not shadow outer class \"static\" or type members"
)]
public static class Endpoints
{
    public static class Health
    {
        public static string Ready() => "health";

        public static string Authorized() => $"{Ready()}/authorized";

        public static string All() => $"{Ready()}/all";
    }

    public static class OpenApi
    {
        public const string V1 = "documentation/openapi/v1.json";
    }

    public static class Admin
    {
        public static string OrganisationComplianceDeclarations(Guid organisationId, EndpointQuery? query = null) =>
            $"admin/organisations/{organisationId}/compliance-declarations{query}";

        public static string AuditEvents(EndpointQuery? query = null) => $"admin/audit-events{query}";

        public static string AuditEventCounter() => "admin/audit-events/counter";

        public static string UnsubmittedPollingStatus() => "admin/unsubmitted-compliance-declarations/polling-status";

        public static string UnsubmittedPollingVolume() => "admin/unsubmitted-compliance-declarations/polling-volume";

        public static string UnsubmittedPollingPlan() => "admin/unsubmitted-compliance-declarations/polling-plan";

        public static string UnsubmittedHistoricalBackfill() =>
            "admin/unsubmitted-compliance-declarations/historical-backfill";

        public static string UnsubmittedReferenceResolutionIssues() =>
            "admin/unsubmitted-compliance-declarations/reference-resolution-issues";

        public static string OrganisationEligibilitySnapshot() =>
            "admin/unsubmitted-compliance-declarations/eligibility-snapshot";

        public static string OrganisationEligibility(EndpointQuery? query = null) =>
            $"admin/unsubmitted-compliance-declarations/eligibility{query}";

        public static string OrganisationObligationSummaries(Guid organisationId) =>
            $"admin/organisations/{organisationId}/obligation-summaries";

        public static string FailedOrganisationObligationSummaries(int obligationYear) =>
            $"admin/unsubmitted-compliance-declarations/failed-obligation-summaries?obligationYear={obligationYear}";

        public static string OrganisationObligationHistoricalBackfills() =>
            "admin/unsubmitted-compliance-declarations/historical-backfills";

        public static string OrganisationObligationRequestPacingState() =>
            "admin/unsubmitted-compliance-declarations/request-pacing";

        public static string UnsubmittedOrganisationDetails(Guid organisationId, bool includeLiveData = false) =>
            $"admin/unsubmitted-compliance-declarations/organisations/{organisationId}"
            + (includeLiveData ? "?includeLiveData=true" : string.Empty);
    }

    public static class Organisations
    {
        private static string Root => "organisations";

        public static string Read(Guid id) => $"{Root}/{id}";

        public static class Obligations
        {
            private static string Root = "obligations";

            public static string Read(Guid organisationId, EndpointQuery? query = null) =>
                $"{Organisations.Read(organisationId)}/{Root}{query}";
        }

        public static class Prns
        {
            private static string Root = "prns";

            public static string Search(Guid organisationId, EndpointQuery? query = null) =>
                $"{Organisations.Read(organisationId)}/{Root}{query}";

            public static string Read(Guid organisationId, string prnId) =>
                $"{Organisations.Read(organisationId)}/{Root}/{prnId}";

            public static string Update(Guid organisationId, string prnId) => Read(organisationId, prnId);
        }

        public static class ComplianceDeclarations
        {
            private static string Root = "compliance-declarations";

            public static string Create(Guid organisationId) => $"{Organisations.Read(organisationId)}/{Root}";

            public static string Read(Guid organisationId, EndpointQuery? query = null) =>
                $"{Create(organisationId)}{query}";

            public static string Read(Guid organisationId, string complianceDeclarationId) =>
                $"{Create(organisationId)}/{complianceDeclarationId}";

            public static string Update(Guid organisationId, string complianceDeclarationId) =>
                $"{Read(organisationId, complianceDeclarationId)}";
        }
    }

    public static class ComplianceDeclarations
    {
        private static string Root = "compliance-declarations";

        public static string Search(EndpointQuery? query = null) => $"{Root}{query}";

        public static string Unsubmitted(EndpointQuery? query = null) => $"{Root}/unsubmitted{query}";

        public static string Delete(string id) => $"{Root}/{id}";
    }
}
