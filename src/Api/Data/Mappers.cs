using Defra.WasteObligations.Api.Data.Entities;

namespace Defra.WasteObligations.Api.Data;

public static class Mappers
{
    public static Dtos.ComplianceDeclarationsPaged ToDto(
        this ComplianceDeclarationSearchResult result,
        int page,
        int pageSize
    ) =>
        new()
        {
            ComplianceDeclarations = result.ComplianceDeclarations.Select(x => x.ToDto()),
            Page = page,
            PageSize = pageSize,
            Total = result.Total,
        };
}
