using System.Text.Json;

namespace Defra.WasteObligations.Api.Utils.ErrorHandling;

public class InvalidRequestBodyException(string message) : JsonException(message);
