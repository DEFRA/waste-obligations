using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.OrganisationEligibility;
using EntityRegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;
using OrganisationReferenceNumberResolutionState = Defra.WasteObligations.Api.Data.Entities.OrganisationReferenceNumberResolutionState;
using OrganisationRegistrationStatus = Defra.WasteObligations.Api.Data.Entities.OrganisationRegistrationStatus;
using Registration = Defra.WasteObligations.Api.Services.WasteOrganisations.Registration;
using WasteOrganisationsAddress = Defra.WasteObligations.Api.Services.WasteOrganisations.Address;
using WasteOrganisationsRegistrationStatus = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationStatus;
using WasteOrganisationsRegistrationType = Defra.WasteObligations.Api.Services.WasteOrganisations.RegistrationType;

namespace Defra.WasteObligations.Api.Tests.Services.OrganisationEligibility;

public class MappersTests
{
    private static readonly DateTimeOffset s_refreshedAt = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToEligibilityRows_ShouldMapAllRelevantRegistrationsIncludingCancelled()
    {
        var organisationId = Guid.NewGuid();
        var organisation = CreateOrganisation(
            organisationId,
            [
                CreateRegistration(
                    WasteOrganisationsRegistrationType.LargeProducer,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2025
                ),
                CreateRegistration(
                    WasteOrganisationsRegistrationType.LargeProducer,
                    WasteOrganisationsRegistrationStatus.Cancelled,
                    2026
                ),
                CreateRegistration(
                    WasteOrganisationsRegistrationType.ComplianceScheme,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2026
                ),
                CreateRegistration("SMALL_PRODUCER", WasteOrganisationsRegistrationStatus.Registered, 2026),
            ]
        );

        var rows = Mappers.ToEligibilityRows([organisation], "g1", s_refreshedAt);

        rows.Should()
            .BeEquivalentTo(
                [
                    new
                    {
                        OrganisationId = organisationId,
                        ObligationYear = 2025,
                        RegistrationType = EntityRegistrationType.DirectProducer,
                        RegistrationStatus = OrganisationRegistrationStatus.Registered,
                    },
                    new
                    {
                        OrganisationId = organisationId,
                        ObligationYear = 2026,
                        RegistrationType = EntityRegistrationType.DirectProducer,
                        RegistrationStatus = OrganisationRegistrationStatus.Cancelled,
                    },
                    new
                    {
                        OrganisationId = organisationId,
                        ObligationYear = 2026,
                        RegistrationType = EntityRegistrationType.ComplianceScheme,
                        RegistrationStatus = OrganisationRegistrationStatus.Registered,
                    },
                ],
                options => options.ExcludingMissingMembers()
            );
        rows.Should()
            .OnlyContain(x =>
                x.Generation == "g1"
                && x.Name == "Example Organisation"
                && x.TradingName == "Example Trading Name"
                && x.CompaniesHouseNumber == "12345678"
                && x.ReferenceNumber == null
                && x.ReferenceNumberResolutionState == OrganisationReferenceNumberResolutionState.Pending
                && x.RefreshedAt == s_refreshedAt.UtcDateTime
            );
    }

    [Fact]
    public void ToEligibilityRows_ShouldProduceTheSameFingerprintWhenSourceOrderChanges()
    {
        var firstOrganisation = CreateOrganisation(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            [
                CreateRegistration(
                    WasteOrganisationsRegistrationType.LargeProducer,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2026
                ),
            ]
        );
        var secondOrganisation = CreateOrganisation(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            [
                CreateRegistration(
                    WasteOrganisationsRegistrationType.ComplianceScheme,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2026
                ),
            ]
        );

        var firstRows = Mappers.ToEligibilityRows([firstOrganisation, secondOrganisation], "g1", s_refreshedAt);
        var secondRows = Mappers.ToEligibilityRows(
            [secondOrganisation, firstOrganisation],
            "g2",
            s_refreshedAt.AddMinutes(30)
        );

        firstRows.Select(x => x.SourceFingerprint).Should().BeEquivalentTo(secondRows.Select(x => x.SourceFingerprint));
    }

    [Fact]
    public void ToEligibilityRows_WhenRelevantSourceFieldChanges_ShouldChangeFingerprint()
    {
        var organisation = CreateOrganisation(
            Guid.NewGuid(),
            [
                CreateRegistration(
                    WasteOrganisationsRegistrationType.LargeProducer,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2026
                ),
            ]
        );

        var initialRow = Mappers.ToEligibilityRows([organisation], "g1", s_refreshedAt).Single();
        var changedRow = Mappers
            .ToEligibilityRows([organisation with { Name = "Changed organisation" }], "g2", s_refreshedAt)
            .Single();

        changedRow.SourceFingerprint.Should().NotBe(initialRow.SourceFingerprint);
    }

    [Fact]
    public void ToEligibilityRows_WhenDuplicateRelevantRegistrationExists_ShouldThrow()
    {
        var organisation = CreateOrganisation(
            Guid.NewGuid(),
            [
                CreateRegistration(
                    WasteOrganisationsRegistrationType.LargeProducer,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2026
                ),
                CreateRegistration(
                    WasteOrganisationsRegistrationType.LargeProducer,
                    WasteOrganisationsRegistrationStatus.Cancelled,
                    2026
                ),
            ]
        );

        var act = () => Mappers.ToEligibilityRows([organisation], "g1", s_refreshedAt);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                $"*organisation {organisation.Id:D}, year 2026, type {WasteOrganisationsRegistrationType.LargeProducer}"
            );
    }

    [Fact]
    public void ToEligibilityRows_WhenRelevantRegistrationHasUnknownStatus_ShouldThrow()
    {
        var organisation = CreateOrganisation(
            Guid.NewGuid(),
            [CreateRegistration(WasteOrganisationsRegistrationType.LargeProducer, "WITHDRAWN", 2026)]
        );

        var act = () => Mappers.ToEligibilityRows([organisation], "g1", s_refreshedAt);

        act.Should().Throw<InvalidOperationException>().WithMessage("*WITHDRAWN*");
    }

    [Fact]
    public void ToEligibilityRows_WhenComplianceSchemeHasNoCompaniesHouseNumber_ShouldAwaitLookupKey()
    {
        var organisation = CreateOrganisation(
            Guid.NewGuid(),
            [
                CreateRegistration(
                    WasteOrganisationsRegistrationType.ComplianceScheme,
                    WasteOrganisationsRegistrationStatus.Registered,
                    2026
                ),
            ]
        ) with
        {
            CompaniesHouseNumber = null,
        };

        var row = Mappers.ToEligibilityRows([organisation], "g1", s_refreshedAt).Single();

        row.ReferenceNumberResolutionState.Should().Be(OrganisationReferenceNumberResolutionState.AwaitingLookupKey);
    }

    private static Organisation CreateOrganisation(Guid organisationId, Registration[] registrations) =>
        new()
        {
            Id = organisationId,
            Name = "Example Organisation",
            TradingName = "Example Trading Name",
            CompaniesHouseNumber = "12345678",
            Address = new WasteOrganisationsAddress(),
            Registrations = registrations,
        };

    private static Registration CreateRegistration(string type, string status, int registrationYear) =>
        new()
        {
            Type = type,
            Status = status,
            RegistrationYear = registrationYear,
        };
}
