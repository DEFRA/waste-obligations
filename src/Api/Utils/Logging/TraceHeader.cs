using System.ComponentModel.DataAnnotations;

namespace Defra.WasteObligations.Api.Utils.Logging;

public class TraceHeader
{
    [ConfigurationKeyName("TraceHeader")]
    [Required]
    public required string Name { get; set; }
}
