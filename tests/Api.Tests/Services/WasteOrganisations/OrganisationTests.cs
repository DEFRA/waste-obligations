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
    public void CompanyName_WhenNoYear_ShouldUseLatestRegistration()
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

    [Fact]
    public void CompanyName_WhenRegisteredAndCancelled_ShouldUseRegistered()
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
                            .With(x => x.Status, RegistrationStatus.Cancelled)
                            .With(x => x.RegistrationYear, 2026)
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

        subject.CompanyName(2026).Should().Be("Trading Name");
    }

    [Fact]
    public void CompanyName_WhenOnlyCancelled_ShouldUseCancelled()
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
                            .With(x => x.Status, RegistrationStatus.Cancelled)
                            .With(x => x.RegistrationYear, 2026)
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName(2026).Should().Be("Organisation Name");
    }

    [Theory]
    [InlineData(2026, "No registration found, using year 2026")]
    [InlineData(null, "No registration found, using year 0")]
    public void CompanyName_WhenNoRegistrations_ShouldThrow(int? year, string expectedMessage)
    {
        var subject = OrganisationFixture.Default().With(x => x.Registrations, () => []).Create();

        var act = () => subject.CompanyName(year);

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void CompanyName_WhenMultipleForSameYear_ShouldUseLastUpdated()
    {
        var updated = new DateTimeOffset(2026, 5, 18, 11, 20, 0, TimeSpan.Zero);

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
                            .With(x => x.RegistrationYear, 2026)
                            .With(x => x.Updated, updated)
                            .Create(),
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .With(x => x.RegistrationYear, 2026)
                            .With(x => x.Updated, updated.AddSeconds(10))
                            .Create(),
                    ]
            )
            .Create();

        subject.CompanyName(2026).Should().Be("Trading Name");
    }

    [Fact]
    public void RegistrationType_WhenDirectProducer_ShouldReturnLargeProducer()
    {
        var subject = OrganisationFixture
            .Default()
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

        subject.RegistrationType(2026).Should().Be(RegistrationType.LargeProducer);
    }

    [Fact]
    public void RegistrationType_WhenComplianceScheme_ShouldReturnComplianceScheme()
    {
        var subject = OrganisationFixture
            .Default()
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

        subject.RegistrationType(2026).Should().Be(RegistrationType.ComplianceScheme);
    }

    [Fact]
    public void RegistrationType_WhenNoYear_ShouldUseLatestRegistration()
    {
        var subject = OrganisationFixture
            .Default()
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.LargeProducer)
                            .With(x => x.Status, RegistrationStatus.Registered)
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

        subject.RegistrationType().Should().Be(RegistrationType.ComplianceScheme);
    }

    [Fact]
    public void RegistrationType_WhenRegisteredAndCancelled_ShouldUseRegistered()
    {
        var subject = OrganisationFixture
            .Default()
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.LargeProducer)
                            .With(x => x.Status, RegistrationStatus.Cancelled)
                            .With(x => x.RegistrationYear, 2026)
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

        subject.RegistrationType(2026).Should().Be(RegistrationType.ComplianceScheme);
    }

    [Fact]
    public void RegistrationType_WhenMultipleForSameYear_ShouldUseLastUpdated()
    {
        var updated = new DateTimeOffset(2026, 5, 18, 11, 20, 0, TimeSpan.Zero);

        var subject = OrganisationFixture
            .Default()
            .With(
                x => x.Registrations,
                () =>
                    [
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.LargeProducer)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .With(x => x.RegistrationYear, 2026)
                            .With(x => x.Updated, updated)
                            .Create(),
                        RegistrationFixture
                            .Default()
                            .With(x => x.Type, RegistrationType.ComplianceScheme)
                            .With(x => x.Status, RegistrationStatus.Registered)
                            .With(x => x.RegistrationYear, 2026)
                            .With(x => x.Updated, updated.AddSeconds(10))
                            .Create(),
                    ]
            )
            .Create();

        subject.RegistrationType(2026).Should().Be(RegistrationType.ComplianceScheme);
    }

    [Theory]
    [InlineData(2026, "No registration found, using year 2026")]
    [InlineData(null, "No registration found, using year 0")]
    public void RegistrationType_WhenNoRegistrations_ShouldThrow(int? year, string expectedMessage)
    {
        var subject = OrganisationFixture.Default().With(x => x.Registrations, () => []).Create();

        var act = () => subject.RegistrationType(year);

        act.Should().Throw<InvalidOperationException>().And.Message.Should().Be(expectedMessage);
    }
}
