using Defra.WasteObligations.Api.Data;

namespace Defra.WasteObligations.Api.Dtos;

public static class UnsubmittedOrganisationSortParser
{
    public static UnsubmittedOrganisationSort? Parse(string? value)
    {
        if (value is null)
            return null;

        if (!TryParse(value, out var sort))
            throw new ArgumentException("Invalid unsubmitted organisation sort", nameof(value));

        return sort;
    }

    public static bool TryParse(string value, out UnsubmittedOrganisationSort? sort)
    {
        sort = null;
        if (string.IsNullOrWhiteSpace(value) || value.Contains(','))
            return false;

        var openingBracket = value.IndexOf('[');
        if (openingBracket <= 0 || !value.EndsWith(']'))
            return false;

        var fieldValue = value[..openingBracket];
        if (
            !Enum.TryParse<UnsubmittedOrganisationSortField>(fieldValue, out var field)
            || !Enum.IsDefined(field)
            || fieldValue != field.ToString()
        )
            return false;

        var direction = value[(openingBracket + 1)..^1] switch
        {
            "asc" => UnsubmittedOrganisationSortDirection.Ascending,
            "desc" => UnsubmittedOrganisationSortDirection.Descending,
            _ => (UnsubmittedOrganisationSortDirection?)null,
        };
        if (direction is null)
            return false;

        sort = new UnsubmittedOrganisationSort { Field = field, Direction = direction.Value };

        return true;
    }
}
