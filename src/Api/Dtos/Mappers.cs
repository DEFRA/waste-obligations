namespace Defra.WasteObligations.Api.Dtos;

public static class Mappers
{
    public static Data.Entities.ComplianceDeclaration ToEntity(
        this CreateComplianceDeclarationRequest dto,
        Services.WasteOrganisations.Organisation organisation
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisation.Id,
            ObligationYear = dto.ObligationYear,
            Obligations = dto.Obligations.Select(x => x.ToEntity()).ToList(),
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
}
