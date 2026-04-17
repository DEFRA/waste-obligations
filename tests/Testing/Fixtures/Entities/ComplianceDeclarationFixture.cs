using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Data.Entities;
using Defra.WasteObligations.Api.Dtos;

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
        return Declaration().With(x => x.ObligationYear, 2026);
    }
}
