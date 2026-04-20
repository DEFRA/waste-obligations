using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using ObligationYear = Defra.WasteObligations.Api.Dtos.ObligationYear;

// ReSharper disable ConvertClosureToMethodGroup

namespace Defra.WasteObligations.Testing.Fixtures.Entities;

public static class ComplianceDeclarationFixture
{
    private static Fixture GetFixture() => new();

    private static int RandomObligationYear() => Random.Shared.Next(ObligationYear.Minimum, ObligationYear.Maximum + 1);

    public static IPostprocessComposer<ComplianceDeclaration> Declaration()
    {
        return GetFixture().Build<ComplianceDeclaration>().With(x => x.ObligationYear, () => RandomObligationYear());
    }

    public static IPostprocessComposer<ComplianceDeclaration> Default()
    {
        return Declaration()
            .With(x => x.ObligationYear, 2026)
            .With(x => x.Obligations, [ObligationFixture.Default().Create()])
            .With(x => x.DeclarationText, LocalizedTextFixture.Default().Create())
            .With(x => x.SubmitterName, "Submitter Name")
            .With(x => x.User, UserFixture.Default().Create());
    }
}
