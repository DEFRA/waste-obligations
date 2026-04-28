namespace Defra.WasteObligations.Api.Dtos;

public static class Mappers
{
    public static Data.Entities.ComplianceDeclaration ToEntity(this CreateComplianceDeclarationRequest dto) =>
        new()
        {
            Id = Guid.NewGuid(),
            Organisation = dto.Organisation.ToEntity(),
            ObligationYear = dto.ObligationYear,
            Obligations = dto.Obligations.Select(x => x.ToEntity()).ToList(),
            ObligationStatus = dto.ObligationStatus,
            DeclarationText = dto.DeclarationText.ToEntity(),
            SubmitterName = dto.SubmitterName,
            User = dto.User.ToEntity(),
        };

    private static Data.Entities.Obligation ToEntity(this Obligation dto) =>
        new()
        {
            Material = dto.Material,
            RecyclingTarget = dto.RecyclingTarget,
            Tonnages = dto.Tonnages.ToEntity(),
            Status = dto.Status,
        };

    private static Data.Entities.ObligationTonnages ToEntity(this ObligationTonnages dto) =>
        new()
        {
            Material = dto.Material,
            AwaitingAcceptance = dto.AwaitingAcceptance,
            Accepted = dto.Accepted,
            Outstanding = dto.Outstanding,
            Obligated = dto.Obligated,
        };

    private static Data.Entities.LocalizedText ToEntity(this LocalizedText dto) =>
        new() { Text = dto.Text, Language = dto.Language };

    private static Data.Entities.User ToEntity(this User dto) => new() { Id = dto.Id, Email = dto.Email };

    private static Data.Entities.Organisation ToEntity(this OrganisationRequest dto) =>
        new()
        {
            Id = dto.Id,
            Name = dto.Name,
            ComplianceSchemeName = dto.ComplianceSchemeName,
            SchemeOperatorName = dto.SchemeOperatorName,
            ReferenceNumber = dto.ReferenceNumber,
            Address = dto.Address?.ToEntity(),
            Regulator = dto.Regulator,
        };

    private static Data.Entities.Address ToEntity(this Address dto) =>
        new()
        {
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            Town = dto.Town,
            County = dto.County,
            Postcode = dto.Postcode,
            Country = dto.Country,
        };
}
