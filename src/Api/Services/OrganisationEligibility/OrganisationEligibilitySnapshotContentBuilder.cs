using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public static class OrganisationEligibilitySnapshotContentBuilder
{
    public static OrganisationEligibilitySnapshotContent Create(
        IReadOnlyCollection<Data.Entities.OrganisationEligibility> sourceRows,
        IReadOnlyCollection<OrganisationReferenceCache> referenceCaches
    )
    {
        var referenceCachesByKey = referenceCaches.ToDictionary(x => new OrganisationReferenceCacheKey(
            x.OrganisationId,
            x.RegistrationType
        ));
        var rows = sourceRows
            .Select(row =>
                Materialise(
                    row,
                    referenceCachesByKey.GetValueOrDefault(
                        new OrganisationReferenceCacheKey(row.OrganisationId, row.RegistrationType)
                    )
                )
            )
            .OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ToArray();

        return new OrganisationEligibilitySnapshotContent { Rows = rows, Fingerprint = CalculateFingerprint(rows) };
    }

    private static Data.Entities.OrganisationEligibility Materialise(
        Data.Entities.OrganisationEligibility sourceRow,
        OrganisationReferenceCache? referenceCache
    )
    {
        var referenceNumber =
            referenceCache?.ResolutionState == OrganisationReferenceNumberResolutionState.Resolved
            && !string.IsNullOrWhiteSpace(referenceCache.ReferenceNumber)
                ? referenceCache.ReferenceNumber
                : null;
        var resolutionState = referenceNumber is null
            ? referenceCache?.ResolutionState ?? sourceRow.ReferenceNumberResolutionState
            : OrganisationReferenceNumberResolutionState.Resolved;

        return sourceRow with
        {
            ReferenceNumber = referenceNumber,
            ReferenceNumberResolutionState = resolutionState,
        };
    }

    private static string CalculateFingerprint(Data.Entities.OrganisationEligibility[] rows)
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
