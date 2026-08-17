using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing.Fixtures.Dtos;
using Defra.WasteObligations.Testing.Fixtures.Entities;

namespace Defra.WasteObligations.Api.Tests.Services;

public class ComplianceDeclarationCancellationNotificationParametersTests
{
    [Fact]
    public void Build_ShouldIncludeDeclarationDerivedParameters()
    {
        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer().Create();

        var result = ComplianceDeclarationCancellationNotificationParameters.Build(complianceDeclaration, null);

        result.Count.Should().Be(3);
        result["year"].Should().Be(complianceDeclaration.ObligationYear);
        result["environmentalRegulator"].Should().Be(complianceDeclaration.Organisation.Regulator);
        result["regulatorEmail"].Should().Be(complianceDeclaration.Organisation.RegulatorEmail);
    }

    [Fact]
    public void Build_ShouldMergeCallerParameters()
    {
        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer().Create();
        var callerParameters = NotificationFixture.DirectProducerCancellationParameters();

        var result = ComplianceDeclarationCancellationNotificationParameters.Build(
            complianceDeclaration,
            callerParameters
        );

        result["certOrStatement"].Should().Be("certificate");
        result["certOrStatement_cy"].Should().Be("tystysgrif");
        result["environmentalRegulator_cy"].Should().Be("Regulator");
        result["year"].Should().Be(complianceDeclaration.ObligationYear);
        result["environmentalRegulator"].Should().Be(complianceDeclaration.Organisation.Regulator);
        result["regulatorEmail"].Should().Be(complianceDeclaration.Organisation.RegulatorEmail);
    }

    [Fact]
    public void Build_ShouldAllowCallerToOverrideDeclarationDerivedParameters()
    {
        const string overrideRegulatorEmail = "override@email.com";
        var complianceDeclaration = ComplianceDeclarationFixture.DirectProducer().Create();

        var result = ComplianceDeclarationCancellationNotificationParameters.Build(
            complianceDeclaration,
            new Dictionary<string, string> { ["regulatorEmail"] = overrideRegulatorEmail }
        );

        result["regulatorEmail"].Should().Be(overrideRegulatorEmail);
    }
}
