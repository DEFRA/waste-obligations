using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

public static class ComplianceDeclarationCancellationNotificationParameters
{
    public static Dictionary<string, object> Build(
        ComplianceDeclaration complianceDeclaration,
        IReadOnlyDictionary<string, string>? callerParameters
    )
    {
        var personalisation = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "year", complianceDeclaration.ObligationYear },
            { "environmentalRegulator", complianceDeclaration.Organisation.Regulator },
            { "regulatorEmail", complianceDeclaration.Organisation.RegulatorEmail },
        };

        if (callerParameters is null)
            return personalisation;

        foreach (var (key, value) in callerParameters)
            personalisation[key] = value;

        return personalisation;
    }
}
