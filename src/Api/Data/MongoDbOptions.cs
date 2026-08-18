using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteObligations.Api.Data;

[ExcludeFromCodeCoverage]
public class MongoDbOptions
{
    public const string SectionName = "Mongo";

    [Required]
    public string? DatabaseUri { get; set; }

    [Required]
    public string? DatabaseName { get; set; }

    [Range(1, 120)]
    public int TransactionTimeoutSeconds { get; init; } = 5;

    [Range(0, 10)]
    public int TransactionWriteConflictRetryCount { get; init; } = 5;
}
