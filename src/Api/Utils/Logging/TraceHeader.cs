using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Utils.Logging;

public class TraceHeader
{
    public const string CorrelationIdPropertyName = "CorrelationId";

    [ConfigurationKeyName("TraceHeader")]
    [Required]
    public required string Name { get; set; }
}
