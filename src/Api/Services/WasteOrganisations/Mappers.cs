namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public static class Mappers
{
    public static Dtos.Organisation ToDto(this Organisation organisation) =>
        new()
        {
            Id = organisation.Id,
            CompanyName = organisation.Name,
            Regulator = "Regulator Name",
        };
}
