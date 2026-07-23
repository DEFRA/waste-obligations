using Defra.WasteObligations.Api.Dtos;

namespace Defra.WasteObligations.Api.Services.PrnCommonBackend;

public static class Mappers
{
    public static Dtos.Obligation ToDto(this Obligation obligation) =>
        new()
        {
            Material = obligation.MaterialName,
            RecyclingTarget = obligation.MaterialTarget,
            Tonnages = new ObligationTonnages
            {
                Material = obligation.Tonnage,
                AwaitingAcceptance = obligation.TonnageAwaitingAcceptance,
                Accepted = obligation.TonnageAccepted,
                Outstanding = obligation.TonnageOutstanding.GetValueOrDefault(),
                Obligated = obligation.ObligationToMeet.GetValueOrDefault(),
            },
            Status = obligation.Status,
        };

    public static Dtos.Prn ToDto(this PrnDetails prn) =>
        new()
        {
            Id = Required(prn.ExternalId, nameof(prn.ExternalId)).ToString("D"),
            Number = Required(prn.PrnNumber, nameof(prn.PrnNumber)),
            Type = prn.IsExport ? PrnType.Pern : PrnType.Prn,
            Status = MapStatus(prn.PrnStatus),
            IssuedAt = ToUtcDateTimeOffset(Required(prn.IssueDate, nameof(prn.IssueDate))),
            ObligationYear = ParseYear(prn.ObligationYear, nameof(prn.ObligationYear)),
            AccreditationYear = ParseYear(prn.AccreditationYear, nameof(prn.AccreditationYear)),
            DecemberWaste = prn.DecemberWaste,
            Material = MapMaterial(prn.MaterialName),
            RecyclingProcess = Required(prn.ProcessToBeUsed, nameof(prn.ProcessToBeUsed)),
            Tonnage = Required(prn.TonnageValue, nameof(prn.TonnageValue)),
            Issuer = new PrnIssuer { OrganisationName = Required(prn.IssuedByOrg, nameof(prn.IssuedByOrg)) },
            Recipient = new PrnRecipient
            {
                OrganisationId = Required(prn.OrganisationId, nameof(prn.OrganisationId)),
                DisplayName = Required(prn.OrganisationName, nameof(prn.OrganisationName)),
                Name = null,
                TradingName = null,
                RegistrationType = null,
            },
            AuthorisedBy = new PrnAuthorisedBy
            {
                Name = Optional(prn.PrnSignatory),
                Position = Optional(prn.PrnSignatoryPosition),
            },
            AccreditationNumber = Required(prn.AccreditationNumber, nameof(prn.AccreditationNumber)),
            ReprocessingSite = Optional(prn.ReprocessingSite),
            ReprocessorExporterAgency = Required(prn.ReprocessorExporterAgency, nameof(prn.ReprocessorExporterAgency)),
            AdditionalNotes = Optional(prn.IssuerNotes),
            Audit = new PrnAudit
            {
                CreatedAt = ToUtcDateTimeOffset(Required(prn.CreatedOn, nameof(prn.CreatedOn))),
                UpdatedAt = ToUtcDateTimeOffset(Required(prn.LastUpdatedDate, nameof(prn.LastUpdatedDate))),
                AcceptedAt = null,
                RejectedAt = null,
                CancelledAt = null,
            },
        };

    public static DateTimeOffset ToUtcDateTimeOffset(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };

        return new DateTimeOffset(utcValue);
    }

    private static string MapMaterial(string? material) =>
        Required(material, nameof(PrnDetails.MaterialName)) switch
        {
            Material.Aluminium => Material.Aluminium,
            Material.Plastic => Material.Plastic,
            Material.Steel => Material.Steel,
            Material.Wood => Material.Wood,
            "Wood Composting" => Material.Wood,
            "Paper/board" => Material.Paper,
            "Paper Composting" => Material.Paper,
            Material.Fibre => Material.Fibre,
            "Glass Other" => Material.Glass,
            "Glass Re-melt" => Material.GlassRemelt,
            var value => throw Invalid(nameof(PrnDetails.MaterialName), value),
        };

    private static string MapStatus(string? status) =>
        Required(status, nameof(PrnDetails.PrnStatus)) switch
        {
            "AWAITINGACCEPTANCE" => PrnStatus.AwaitingAcceptance,
            "ACCEPTED" => PrnStatus.Accepted,
            "REJECTED" => PrnStatus.Rejected,
            "CANCELLED" or "CANCELED" => PrnStatus.Cancelled,
            var value => throw Invalid(nameof(PrnDetails.PrnStatus), value),
        };

    private static int ParseYear(string? value, string propertyName)
    {
        if (!int.TryParse(value, out var year) || year == default)
            throw Invalid(propertyName, value);

        return year;
    }

    private static Guid Required(Guid value, string propertyName)
    {
        if (value == Guid.Empty)
            throw Invalid(propertyName, value);

        return value;
    }

    private static DateTime Required(DateTime value, string propertyName)
    {
        if (value == default)
            throw Invalid(propertyName, value);

        return value;
    }

    private static int Required(int value, string propertyName)
    {
        if (value == default)
            throw Invalid(propertyName, value);

        return value;
    }

    private static string Required(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Invalid(propertyName, value);

        return value;
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static InvalidOperationException Invalid(string propertyName, object? value) =>
        new($"PRN common backend returned an invalid {propertyName} value: '{value}'");
}
