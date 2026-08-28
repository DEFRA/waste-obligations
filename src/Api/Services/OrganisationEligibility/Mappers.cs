using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;
using OrganisationComplianceDeclarationEligibilityEntity = Defra.WasteObligations.Api.Data.Entities.OrganisationComplianceDeclarationEligibility;
using OrganisationReferenceNumberResolutionState = Defra.WasteObligations.Api.Data.Entities.OrganisationReferenceNumberResolutionState;
using OrganisationRegistrationStatus = Defra.WasteObligations.Api.Data.Entities.OrganisationRegistrationStatus;
using RegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType;
using WasteOrganisationsRegistrationStatus = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationStatus;
using WasteOrganisationsRegistrationType = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationType;

namespace Defra.WasteObligations.Api.Services.OrganisationEligibility;

public static class Mappers
{
    public static IReadOnlyList<OrganisationComplianceDeclarationEligibilityEntity> ToEligibilityRows(
        IEnumerable<Organisation> organisations,
        string generation,
        DateTimeOffset refreshedAt
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generation);

        var rows = new List<OrganisationComplianceDeclarationEligibilityEntity>();
        var keys = new HashSet<(Guid OrganisationId, int ObligationYear, RegistrationType RegistrationType)>();

        foreach (var organisation in organisations)
        {
            foreach (var registration in organisation.Registrations)
            {
                var registrationType = ToRegistrationType(registration.Type);
                if (registrationType is null)
                    continue;

                var key = (organisation.Id, registration.RegistrationYear, registrationType.Value);
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Waste Organisations returned duplicate registrations for organisation {organisation.Id:D}, "
                            + $"year {registration.RegistrationYear}, type {registration.Type}"
                    );
                }

                var registrationStatus = ToRegistrationStatus(registration.Status);
                rows.Add(
                    new OrganisationComplianceDeclarationEligibilityEntity
                    {
                        Generation = generation,
                        OrganisationId = organisation.Id,
                        ObligationYear = registration.RegistrationYear,
                        RegistrationType = registrationType.Value,
                        RegistrationStatus = registrationStatus,
                        Name = organisation.CompanyName(registration),
                        TradingName = organisation.TradingName,
                        CompaniesHouseNumber = organisation.CompaniesHouseNumber,
                        ReferenceNumber = null,
                        ReferenceNumberResolutionState = InitialReferenceNumberResolutionState(
                            registrationType.Value,
                            organisation.CompaniesHouseNumber
                        ),
                        RecyclingObligationsMet = null,
                        ObligationCoveragePercentage = 0,
                        SourceFingerprint = CalculateSourceFingerprint(
                            organisation,
                            registration.RegistrationYear,
                            registrationType.Value,
                            registrationStatus
                        ),
                        RefreshedAt = refreshedAt.UtcDateTime,
                    }
                );
            }
        }

        return rows.OrderBy(x => x.OrganisationId)
            .ThenBy(x => x.ObligationYear)
            .ThenBy(x => x.RegistrationType)
            .ToArray();
    }

    private static RegistrationType? ToRegistrationType(string sourceRegistrationType) =>
        sourceRegistrationType switch
        {
            WasteOrganisationsRegistrationType.LargeProducer => RegistrationType.DirectProducer,
            WasteOrganisationsRegistrationType.ComplianceScheme => RegistrationType.ComplianceScheme,
            _ => null,
        };

    private static OrganisationRegistrationStatus ToRegistrationStatus(string sourceRegistrationStatus) =>
        sourceRegistrationStatus switch
        {
            WasteOrganisationsRegistrationStatus.Registered => OrganisationRegistrationStatus.Registered,
            WasteOrganisationsRegistrationStatus.Cancelled => OrganisationRegistrationStatus.Cancelled,
            _ => throw new InvalidOperationException(
                $"Unsupported Waste Organisations registration status '{sourceRegistrationStatus}'"
            ),
        };

    private static OrganisationReferenceNumberResolutionState InitialReferenceNumberResolutionState(
        RegistrationType registrationType,
        string? companiesHouseNumber
    ) =>
        registrationType == RegistrationType.ComplianceScheme && string.IsNullOrWhiteSpace(companiesHouseNumber)
            ? OrganisationReferenceNumberResolutionState.AwaitingLookupKey
            : OrganisationReferenceNumberResolutionState.Pending;

    private static string CalculateSourceFingerprint(
        Organisation organisation,
        int obligationYear,
        RegistrationType registrationType,
        OrganisationRegistrationStatus registrationStatus
    )
    {
        var source = string.Concat(
            "organisation-eligibility-source-v1",
            LengthPrefix(organisation.Id.ToString("D")),
            LengthPrefix(obligationYear.ToString(CultureInfo.InvariantCulture)),
            LengthPrefix(registrationType.ToString()),
            LengthPrefix(registrationStatus.ToString()),
            LengthPrefix(organisation.Name),
            LengthPrefix(organisation.TradingName),
            LengthPrefix(organisation.CompaniesHouseNumber)
        );

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static string LengthPrefix(string? value) =>
        value is null ? "-1:" : $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";
}
