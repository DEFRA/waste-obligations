using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class PrnFixture
{
    public static readonly Guid OrganisationId = new("923fa611-571c-4948-ab7d-fbb75e75ed65");
    public const string PrnId = "0d2f531d-0213-494b-8c8b-4133051bd44f";

    private static Fixture GetFixture() => new();

    public static IPostprocessComposer<Prn> Prn()
    {
        return GetFixture().Build<Prn>();
    }

    public static IPostprocessComposer<Prn> Default()
    {
        return Prn()
            .With(x => x.Id, PrnId)
            .With(x => x.Number, "PRN123")
            .With(x => x.Type, PrnType.Prn)
            .With(x => x.Status, PrnStatus.AwaitingAcceptance)
            .With(x => x.IssuedAt, new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero))
            .With(x => x.ObligationYear, 2025)
            .With(x => x.AccreditationYear, 2025)
            .With(x => x.DecemberWaste, false)
            .With(x => x.Material, Material.Aluminium)
            .With(x => x.RecyclingProcess, "R3")
            .With(x => x.Tonnage, 999)
            .With(x => x.Issuer, new PrnIssuer { OrganisationName = "Acme Reprocessors Ltd" })
            .With(
                x => x.Recipient,
                new PrnRecipient
                {
                    OrganisationId = OrganisationId,
                    DisplayName = "Test Producer Ltd",
                    Name = null,
                    TradingName = null,
                    RegistrationType = null,
                }
            )
            .With(x => x.AuthorisedBy, new PrnAuthorisedBy { Name = "Jane Smith", Position = "Director" })
            .With(x => x.AccreditationNumber, "ACC123")
            .With(x => x.ReprocessingSite, "42 Factory Road, Manchester")
            .With(x => x.ReprocessorExporterAgency, "Environment Agency")
            .With(x => x.AdditionalNotes, "Important note about this PRN")
            .With(
                x => x.Audit,
                new PrnAudit
                {
                    CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
                    UpdatedAt = new DateTimeOffset(2026, 1, 15, 10, 5, 0, TimeSpan.Zero),
                    AcceptedAt = null,
                    RejectedAt = null,
                    CancelledAt = null,
                }
            );
    }
}
