using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services.WasteOrganisations;
using Defra.WasteObligations.Testing.Fixtures.WasteOrganisations;

namespace Defra.WasteObligations.Api.Tests.Services.WasteOrganisations;

public class OrganisationTests
{
    [Fact]
    public void CompanyName_WhenDirectProducer_ShouldBeName()
    {
        var subject = OrganisationFixture
            .Default()
            .With(x => x.Name, "Organisation Name")
            .With(x => x.TradingName, "Trading Name")
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.LargeProducer)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName(2026).Should().Be("Organisation Name");
    }

    [Fact]
    public void CompanyName_WhenUnknownType_ShouldBeName()
    {
        var subject = OrganisationFixture
            .Default()
            .With(x => x.Name, "Organisation Name")
            .With(x => x.TradingName, "Trading Name")
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, "Unknown")
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName(2026).Should().Be("Organisation Name");
    }

    [Fact]
    public void CompanyName_WhenComplianceScheme_ShouldBeTradingName()
    {
        var subject = OrganisationFixture
            .Default()
            .With(x => x.Name, "Organisation Name")
            .With(x => x.TradingName, "Trading Name")
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName(2026).Should().Be("Trading Name");
    }

    [Fact]
    public void CompanyName_WhenComplianceScheme_AndTradingNameIsNull_ShouldBeName()
    {
        var subject = OrganisationFixture
            .Default()
            .With(x => x.Name, "Organisation Name")
            .With(x => x.TradingName, (string?)null)
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName(2026).Should().Be("Organisation Name");
    }

    [Fact]
    public void CompanyName_WhenRegistrationIsCancelled_ShouldThrow()
    {
        var subject = OrganisationFixture
            .Default()
            .With(
                x => x.Registrations,
                () => [RegistrationFixture.Default().With(x => x.Status, RegistrationStatus.Cancelled).Create()]
            )
            .Create();

        var act = () => subject.CompanyName(2026);

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be("Registration is cancelled");
    }

    [Fact]
    public void CompanyName_WhenNoYear_ShouldUseLatestRegistration()
    {
        var subject = OrganisationFixture
            .Default()
            .With(x => x.TradingName, "Trading Name")
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.Status, RegistrationStatus.Cancelled)
                            .With(x => x.RegistrationYear, 2025)
                            .Create(),
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .With(x => x.RegistrationYear, 2026)
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName().Should().Be("Trading Name");
    }

    [Theory]
    [InlineData(2026, "No registration found for year \"2026\"")]
    [InlineData(null, "No registration found")]
    public void CompanyName_WhenNoRegistrations_ShouldThrow(int? year, string expectedMessage)
    {
        var subject = OrganisationFixture.Default().With(x => x.Registrations, () => []).Create();

        var act = () => subject.CompanyName(year);

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be(expectedMessage);
    }
}
