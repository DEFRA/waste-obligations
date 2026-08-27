using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Services;

internal sealed record ComplianceDeclarationReviewStateKey(
    Guid OrganisationId,
    int ObligationYear,
    RegistrationType RegistrationType
)
{
    public static ComplianceDeclarationReviewStateKey From(ComplianceDeclaration declaration) =>
        new(declaration.Organisation.Id, declaration.ObligationYear, declaration.Organisation.RegistrationType);

    public static ComplianceDeclarationReviewStateKey From(ComplianceDeclarationReviewState state) =>
        new(state.OrganisationId, state.ObligationYear, state.RegistrationType);
}
