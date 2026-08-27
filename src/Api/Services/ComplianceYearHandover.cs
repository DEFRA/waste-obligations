namespace Defra.WasteObligations.Api.Services;

public record ComplianceYearHandover(
    int CurrentComplianceYear,
    int? IncomingComplianceYear = null,
    int? OutgoingComplianceYear = null,
    DateTime? OutgoingYearCutoverAt = null
);
