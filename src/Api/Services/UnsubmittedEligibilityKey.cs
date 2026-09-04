using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

internal sealed record UnsubmittedEligibilityKey(
    Guid OrganisationId,
    int ObligationYear,
    RegistrationType RegistrationType
)
{
    public static UnsubmittedEligibilityKey From(ComplianceDeclaration declaration) =>
        new(declaration.Organisation.Id, declaration.ObligationYear, declaration.Organisation.RegistrationType);
}
