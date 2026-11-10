namespace Defra.WasteObligations.Api.Data;

public class ConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
