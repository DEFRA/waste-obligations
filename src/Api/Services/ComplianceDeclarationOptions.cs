using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Services;

public class ComplianceDeclarationOptions
{
    public const string SectionName = "ComplianceDeclaration";

    [Range(1, 120)]
    public int TransactionTimeoutSeconds { get; init; } = 5;
}
