namespace Defra.WasteObligations.Api.Services;

public static class BackgroundOperationCleanup
{
    public static async Task Run(params Func<Task>[] steps)
    {
        List<Exception> failures = [];
        foreach (var step in steps)
        {
            try
            {
                await step();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        if (failures.Count > 0)
            throw new AggregateException("Background operation cleanup failed after draining.", failures);
    }
}
