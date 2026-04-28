using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Extensions;
using ComplianceDeclaration = Defra.WasteObligations.Api.Data.Entities.ComplianceDeclaration;
using Obligation = Defra.WasteObligations.Api.Data.Entities.Obligation;
using ObligationYear = Defra.WasteObligations.Api.Dtos.ObligationYear;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class ComplianceDeclarationFixture
{
    private static Fixture GetFixture() => new();

    private static int RandomObligationYear() => Random.Shared.Next(ObligationYear.Minimum, ObligationYear.Maximum + 1);

    public static IPostprocessComposer<ComplianceDeclaration> AddDefaults(
        this ICustomizationComposer<ComplianceDeclaration> composer
    )
    {
        return composer
            .With(x => x.ObligationYear, () => RandomObligationYear())
            .With(x => x.ObligationStatus, () => ObligationStatus.MetOrNot.Random());
    }

    public static IPostprocessComposer<ComplianceDeclaration> Declaration()
    {
        var fixture = GetFixture();

        fixture.Customize<Obligation>(x => x.AddDefaults());

        return fixture.Build<ComplianceDeclaration>().AddDefaults();
    }

    public static IPostprocessComposer<ComplianceDeclaration> Default()
    {
        return Declaration()
            .With(x => x.ObligationYear, 2026)
            .With(x => x.Obligations, [ObligationFixture.Default().Create()])
            .With(x => x.ObligationStatus, ObligationStatus.NotMet)
            .With(x => x.DeclarationText, LocalizedTextFixture.Default().Create())
            .With(x => x.SubmitterName, "Submitter Name")
            .With(x => x.User, UserFixture.Default().Create());
    }

    public static IPostprocessComposer<ComplianceDeclaration> DirectProducer(Guid? organisationId = null)
    {
        return Default().With(x => x.Organisation, OrganisationFixture.DirectProducer(organisationId).Create());
    }

    public static IPostprocessComposer<ComplianceDeclaration> ComplianceScheme(Guid? organisationId = null)
    {
        return Default().With(x => x.Organisation, OrganisationFixture.ComplianceScheme(organisationId).Create());
    }
}
