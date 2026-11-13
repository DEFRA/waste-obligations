namespace Defra.WasteObligations.AuditEvents.Logging;

internal sealed class CorrelationScope(Action restore, IDisposable? logScope) : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        restore();
        logScope?.Dispose();
    }
}
