using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public static class OrganisationEligibilitySnapshotContentBuilder
{
    public static OrganisationEligibilitySnapshotContent Create(
        IReadOnlyCollection<Data.Entities.OrganisationComplianceDeclarationEligibility> rows
    )
    {
        var orderedRows = rows.OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ToArray();

        return new OrganisationEligibilitySnapshotContent
        {
            Rows = orderedRows,
            Fingerprint = CalculateFingerprint(orderedRows),
        };
    }

    private static string CalculateFingerprint(Data.Entities.OrganisationComplianceDeclarationEligibility[] rows)
    {
        var content = new StringBuilder("organisation-eligibility-content-v1");
        Append(content, rows.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var row in rows)
        {
            Append(content, row.OrganisationId.ToString("D"));
            Append(content, row.ObligationYear.ToString(CultureInfo.InvariantCulture));
            Append(content, row.RegistrationType.ToString());
            Append(content, row.SourceFingerprint);
            Append(
                content,
                row.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Resolved
                    ? row.ReferenceNumber
                    : "Unresolved"
            );
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString())));
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
