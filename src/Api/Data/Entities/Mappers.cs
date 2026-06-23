namespace Defra.WasteObligations.Api.Data.Entities;

public static class Mappers
{
    public static Dtos.ComplianceDeclaration ToDto(this ComplianceDeclaration entity) =>
        new()
        {
            Id = entity.Id.ToString(),
            Created = entity.Created,
            Updated = entity.Updated,
            Status = entity.Status switch
            {
                ComplianceDeclarationStatus.Submitted => Dtos.ComplianceDeclarationStatus.Submitted,
                ComplianceDeclarationStatus.Accepted => Dtos.ComplianceDeclarationStatus.Accepted,
                ComplianceDeclarationStatus.Cancelled => Dtos.ComplianceDeclarationStatus.Cancelled,
                _ => throw new InvalidOperationException("Unknown status"),
            },
            Organisation = entity.Organisation.ToDto(),
            ObligationYear = entity.ObligationYear,
            Obligations = entity.Obligations.Select(x => x.ToDto()).ToList(),
            ObligationStatus = entity.ObligationStatus,
            SubmitterName = entity.SubmitterName,
            IsRegulation43Compliant = entity.IsRegulation43Compliant,
            Audit = entity.Audit.Select(x => x.ToDto()).ToList(),
        };

    private static Dtos.Obligation ToDto(this Obligation entity) =>
        new()
        {
            Material = entity.Material,
            RecyclingTarget = entity.RecyclingTarget,
            Tonnages = entity.Tonnages.ToDto(),
            Status = entity.Status,
        };

    private static Dtos.ObligationTonnages ToDto(this ObligationTonnages entity) =>
        new()
        {
            Material = entity.Material,
            AwaitingAcceptance = entity.AwaitingAcceptance,
            Accepted = entity.Accepted,
            Outstanding = entity.Outstanding,
            Obligated = entity.Obligated,
        };

    private static Dtos.User ToDto(this User entity) => new() { Id = entity.Id, Email = entity.Email };

    public static Dtos.Organisation ToDto(this Organisation entity) =>
        new()
        {
            Id = entity.Id,
            RegistrationType = entity.RegistrationType switch
            {
                RegistrationType.DirectProducer => Dtos.RegistrationType.DirectProducer,
                RegistrationType.ComplianceScheme => Dtos.RegistrationType.ComplianceScheme,
                _ => throw new InvalidOperationException("Unknown registration type"),
            },
            Name = entity.Name,
            ComplianceSchemeName = entity.ComplianceSchemeName,
            SchemeOperatorName = entity.SchemeOperatorName,
            ReferenceNumber = entity.ReferenceNumber,
            Address = entity.Address?.ToDto(),
            Regulator = entity.Regulator,
            RegulatorEmail = entity.RegulatorEmail,
        };

    private static Dtos.Address ToDto(this Address entity) =>
        new()
        {
            AddressLine1 = entity.AddressLine1,
            AddressLine2 = entity.AddressLine2,
            Town = entity.Town,
            County = entity.County,
            Postcode = entity.Postcode,
            Country = entity.Country,
        };

    private static Dtos.AuditEntry ToDto(this AuditEntry entity) =>
        entity switch
        {
            ReasonAuditEntry s => new Dtos.ReasonAuditEntry
            {
                User = s.User.ToDto(),
                Timestamp = s.Timestamp,
                Action = s.Action,
                Reason = s.Reason,
            },
            _ => new Dtos.AuditEntry
            {
                User = entity.User.ToDto(),
                Timestamp = entity.Timestamp,
                Action = entity.Action,
            },
        };
}
