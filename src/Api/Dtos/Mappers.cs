using Defra.WasteObligations.Api.Data;

namespace Defra.WasteObligations.Api.Dtos;

public static class Mappers
{
    public static Data.Entities.ComplianceDeclaration ToEntity(
        this CreateComplianceDeclarationRequest dto,
        TimeProvider? timeProvider
    )
    {
        timeProvider ??= TimeProvider.System;

        var draft = new Data.Entities.ComplianceDeclaration
        {
            Organisation = dto.Organisation.ToEntity(),
            ObligationYear = dto.ObligationYear,
            Obligations = [.. dto.Obligations.Select(x => x.ToEntity())],
            ObligationStatus = dto.ObligationStatus,
            DeclarationText = dto.DeclarationText.ToEntity(),
            SubmitterName = dto.SubmitterName,
            IsRegulation43Compliant = dto.IsRegulation43Compliant,
        };

        return draft.Submit(dto.User.ToEntity(), timeProvider.GetUtcNowWithoutMicroseconds());
    }

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

    public static Data.Entities.User ToEntity(this User dto) => new() { Id = dto.Id, Email = dto.Email };

    private static Data.Entities.Organisation ToEntity(this Organisation dto) =>
        new()
        {
            Id = dto.Id,
            RegistrationType = dto.RegistrationType.ToEntity(),
            Name = dto.Name,
            ComplianceSchemeName = dto.ComplianceSchemeName,
            SchemeOperatorName = dto.SchemeOperatorName,
            ReferenceNumber = dto.ReferenceNumber,
            Address = dto.Address?.ToEntity(),
            Regulator = dto.Regulator,
            RegulatorEmail = dto.RegulatorEmail,
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

    public static Data.Entities.ComplianceDeclarationStatus ToEntity(this ComplianceDeclarationStatus dto) =>
        dto switch
        {
            ComplianceDeclarationStatus.Submitted => Data.Entities.ComplianceDeclarationStatus.Submitted,
            ComplianceDeclarationStatus.Accepted => Data.Entities.ComplianceDeclarationStatus.Accepted,
            ComplianceDeclarationStatus.Cancelled => Data.Entities.ComplianceDeclarationStatus.Cancelled,
            _ => throw new InvalidOperationException("Unknown status"),
        };

    public static Data.Entities.RegistrationType ToEntity(this RegistrationType dto) =>
        dto switch
        {
            RegistrationType.DirectProducer => Data.Entities.RegistrationType.DirectProducer,
            RegistrationType.ComplianceScheme => Data.Entities.RegistrationType.ComplianceScheme,
            _ => throw new InvalidOperationException("Unknown registration type"),
        };
}
