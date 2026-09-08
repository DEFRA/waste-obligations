using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;
using NSubstitute;
using Organisation = Defra.WasteObligations.Api.Services.WasteOrganisations.Organisation;
using Registration = Defra.WasteObligations.Api.Services.WasteOrganisations.Registration;

namespace Defra.WasteObligations.Api.Tests.Services;

public class UnsubmittedPollingVolumeServiceTests
{
    private IWasteOrganisationsService WasteOrganisationsService { get; } =
        Substitute.For<IWasteOrganisationsService>();
    private ICurrentObligationYearProvider CurrentObligationYearProvider { get; } =
        Substitute.For<ICurrentObligationYearProvider>();

    [Fact]
    public async Task Get_ShouldGroupRegisteredSupportedOrganisationVolumeByObligationYear()
    {
        const int currentObligationYear = 2026;
        var firstOrganisationId = Guid.NewGuid();
        var secondOrganisationId = Guid.NewGuid();
        WasteOrganisationsService
            .Search(Arg.Any<CancellationToken>())
            .Returns(
                new OrganisationSearch
                {
                    Organisations =
                    [
                        CreateOrganisation(
                            firstOrganisationId,
                            [
                                CreateRegistration(RegistrationType.LargeProducer, RegistrationStatus.Registered, 2026),
                                CreateRegistration(
                                    RegistrationType.ComplianceScheme,
                                    RegistrationStatus.Registered,
                                    2026
                                ),
                                CreateRegistration(RegistrationType.LargeProducer, RegistrationStatus.Cancelled, 2025),
                            ]
                        ),
                        CreateOrganisation(
                            secondOrganisationId,
                            [
                                CreateRegistration(RegistrationType.LargeProducer, RegistrationStatus.Registered, 2026),
                                CreateRegistration(
                                    RegistrationType.ComplianceScheme,
                                    RegistrationStatus.Registered,
                                    2025
                                ),
                            ]
                        ),
                        CreateOrganisation(
                            Guid.NewGuid(),
                            [CreateRegistration("SMALL_PRODUCER", RegistrationStatus.Registered, 2026)]
                        ),
                    ],
                }
            );
        CurrentObligationYearProvider.GetCurrentObligationYear().Returns(currentObligationYear);
        var subject = new UnsubmittedPollingVolumeService(WasteOrganisationsService, CurrentObligationYearProvider);

        var result = await subject.Get(TestContext.Current.CancellationToken);

        result
            .Should()
            .BeEquivalentTo(
                new
                {
                    CurrentObligationYear = currentObligationYear,
                    SourceOrganisationCount = 3,
                    Years = new[]
                    {
                        new
                        {
                            ObligationYear = 2025,
                            IsCurrentObligationYear = false,
                            RegisteredOrganisationCount = 1,
                            RegisteredRegistrationCount = 1,
                        },
                        new
                        {
                            ObligationYear = 2026,
                            IsCurrentObligationYear = true,
                            RegisteredOrganisationCount = 2,
                            RegisteredRegistrationCount = 3,
                        },
                    },
                }
            );
        await WasteOrganisationsService.Received(1).Search(TestContext.Current.CancellationToken);
    }

    private static Organisation CreateOrganisation(Guid organisationId, Registration[] registrations) =>
        OrganisationFixture.Default(organisationId).With(x => x.Registrations, registrations).Create();

    private static Registration CreateRegistration(string type, string status, int registrationYear) =>
        RegistrationFixture
            .Default()
            .With(x => x.Type, type)
            .With(x => x.Status, status)
            .With(x => x.RegistrationYear, registrationYear)
            .Create();
}
