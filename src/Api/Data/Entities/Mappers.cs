namespace Defra.WasteObligations.Api.Data.Entities;

public static class Mappers
{
    public static Dtos.ComplianceDeclaration ToDto(this ComplianceDeclaration entity) =>
        new()
        {
            Id = entity.Id,
            Created = entity.Created,
            Updated = entity.Updated,
            Status = entity.Status switch
            {
                ComplianceDeclarationStatus.Submitted => Dtos.ComplianceDeclarationStatus.Submitted,
                _ => throw new InvalidOperationException("Unknown status"),
            },
            Organisation = entity.Organisation.ToDto(),
            ObligationYear = entity.ObligationYear,
            Obligations = entity.Obligations.Select(x => x.ToDto()).ToList(),
            DeclarationText = entity.DeclarationText.ToDto(),
            SubmitterName = entity.SubmitterName,
            User = entity.User.ToDto(),
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

    private static Dtos.LocalizedText ToDto(this LocalizedText entity) =>
        new() { Text = entity.Text, Language = entity.Language };

    private static Dtos.User ToDto(this User entity) => new() { Id = entity.Id, Email = entity.Email };

    private static Dtos.OrganisationRequest ToDto(this Organisation entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            ComplianceSchemeName = entity.ComplianceSchemeName,
            SchemeOperatorName = entity.SchemeOperatorName,
            ReferenceNumber = entity.ReferenceNumber,
            Address = entity.Address?.ToDto(),
            Regulator = entity.Regulator,
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
}
