using Defra.WasteObligations.Api.Data;

namespace Defra.WasteObligations.Api.Dtos;

public static class ComplianceDeclarationSortParser
{
    public static ComplianceDeclarationSort[] Parse(string? value)
    {
        if (value is null)
            return [];

        if (!TryParse(value, out var sort))
            throw new ArgumentException("Invalid compliance declaration sort", nameof(value));

        return sort;
    }

    public static bool TryParse(string value, out ComplianceDeclarationSort[] sort)
    {
        sort = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var fields = new HashSet<ComplianceDeclarationSortField>();
        var parsedSort = new List<ComplianceDeclarationSort>();

        foreach (var term in value.Split(','))
        {
            var openingBracket = term.IndexOf('[');
            if (openingBracket <= 0 || !term.EndsWith(']'))
                return false;

            var fieldValue = term[..openingBracket];
            if (
                !Enum.TryParse<ComplianceDeclarationSortField>(fieldValue, out var field)
                || !Enum.IsDefined(field)
                || fieldValue != field.ToString()
                || !fields.Add(field)
            )
                return false;

            var direction = term[(openingBracket + 1)..^1] switch
            {
                "asc" => ComplianceDeclarationSortDirection.Ascending,
                "desc" => ComplianceDeclarationSortDirection.Descending,
                _ => (ComplianceDeclarationSortDirection?)null,
            };

            if (direction is null)
                return false;

            parsedSort.Add(new ComplianceDeclarationSort { Field = field, Direction = direction.Value });
        }

        sort = [.. parsedSort];

        return true;
    }
}
