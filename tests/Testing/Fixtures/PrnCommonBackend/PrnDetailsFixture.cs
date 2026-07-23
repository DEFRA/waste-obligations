using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Services.PrnCommonBackend;

namespace Defra.WasteObligations.Testing.Fixtures.PrnCommonBackend;

public static class PrnDetailsFixture
{
    public static readonly Guid PrnId = new("0d2f531d-0213-494b-8c8b-4133051bd44f");
    public static readonly Guid OrganisationId = new("0d2f531d-0213-494b-8c8b-4133051bd44e");

    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<PrnDetails> PrnDetails()
    {
        return GetFixture().Build<PrnDetails>();
    }

    public static IPostprocessComposer<PrnDetails> Default()
    {
        return PrnDetails()
            .With(x => x.ExternalId, PrnId)
            .With(x => x.PrnNumber, "PRN123")
            .With(x => x.OrganisationId, OrganisationId)
            .With(x => x.OrganisationName, "Test Producer Ltd")
            .With(x => x.ReprocessorExporterAgency, "Environment Agency")
            .With(x => x.PrnStatus, "AWAITINGACCEPTANCE")
            .With(x => x.TonnageValue, 999)
            .With(x => x.MaterialName, "Aluminium")
            .With(x => x.IssuerNotes, "Important note about this PRN")
            .With(x => x.PrnSignatory, "Jane Smith")
            .With(x => x.PrnSignatoryPosition, "Director")
            .With(x => x.IssueDate, new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Unspecified))
            .With(x => x.ProcessToBeUsed, "R3")
            .With(x => x.DecemberWaste, false)
            .With(x => x.IssuedByOrg, "Acme Reprocessors Ltd")
            .With(x => x.AccreditationNumber, "ACC123")
            .With(x => x.ReprocessingSite, "42 Factory Road, Manchester")
            .With(x => x.AccreditationYear, "2025")
            .With(x => x.ObligationYear, "2025")
            .With(x => x.CreatedOn, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified))
            .With(x => x.LastUpdatedDate, new DateTime(2026, 1, 15, 10, 5, 0, DateTimeKind.Unspecified))
            .With(x => x.IsExport, false);
    }
}
