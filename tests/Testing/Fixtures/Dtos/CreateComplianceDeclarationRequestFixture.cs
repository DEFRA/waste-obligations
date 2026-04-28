using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Extensions;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class CreateComplianceDeclarationRequestFixture
{
    private static Fixture GetFixture() => new();

    private static int RandomObligationYear() => Random.Shared.Next(ObligationYear.Minimum, ObligationYear.Maximum + 1);

    public static IPostprocessComposer<CreateComplianceDeclarationRequest> AddDefaults(
        this ICustomizationComposer<CreateComplianceDeclarationRequest> composer
    )
    {
        return composer
            .With(x => x.ObligationYear, () => RandomObligationYear())
            .With(x => x.ObligationStatus, () => ObligationStatus.MetOrNot.Random());
    }

    public static IPostprocessComposer<CreateComplianceDeclarationRequest> Request()
    {
        return GetFixture().Build<CreateComplianceDeclarationRequest>().AddDefaults();
    }

    public static IPostprocessComposer<CreateComplianceDeclarationRequest> Default()
    {
        return Request()
            .With(x => x.ObligationYear, 2026)
            .With(x => x.Obligations, [ObligationFixture.Default().Create()])
            .With(x => x.ObligationStatus, ObligationStatus.NotMet)
            .With(x => x.DeclarationText, LocalizedTextFixture.Default().Create())
            .With(x => x.SubmitterName, "Submitter Name")
            .With(x => x.User, UserFixture.Default().Create());
    }

    public static IPostprocessComposer<CreateComplianceDeclarationRequest> DirectProducer(Guid? organisationId = null)
    {
        return Default().With(x => x.Organisation, OrganisationFixture.DirectProducer(organisationId).Create());
    }

    public static IPostprocessComposer<CreateComplianceDeclarationRequest> ComplianceScheme(Guid? organisationId = null)
    {
        return Default().With(x => x.Organisation, OrganisationFixture.ComplianceScheme(organisationId).Create());
    }
}
