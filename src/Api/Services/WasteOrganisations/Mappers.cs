namespace Defra.WasteObligations.Api.Services.WasteOrganisations;

public static class Mappers
{
    public static Dtos.Organisation ToDto(this Organisation organisation, int? year = null) =>
        new()
        {
            Id = organisation.Id,
            CompanyName = organisation.CompanyName(year),
            Regulator = organisation.BusinessCountry switch
            {
                BusinessCountry.England => "Environment Agency",
                BusinessCountry.NorthernIreland => "Northern Ireland Environment Agency",
                BusinessCountry.Scotland => "Scottish Environment Protection Agency",
                BusinessCountry.Wales => "Natural Resources Wales",
                _ => null,
            },
        };
}
