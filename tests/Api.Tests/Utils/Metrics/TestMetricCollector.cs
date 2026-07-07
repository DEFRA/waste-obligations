using System.Diagnostics.Metrics;

namespace Defra.WasteObligations.Api.Tests.Utils.Metrics;

internal sealed class TestMetricCollector<T> : IDisposable
    where T : struct
{
    private readonly MeterListener _listener = new();
    private readonly List<TestMetricMeasurement<T>> _measurements = [];

    public TestMetricCollector(string meterName, string instrumentName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<T>(OnMeasurementRecorded);
        _listener.Start();
    }

    public IReadOnlyList<TestMetricMeasurement<T>> GetMeasurementSnapshot() => _measurements.ToList();

    public void Dispose()
    {
        _listener.Dispose();
    }

    private void OnMeasurementRecorded(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state
    )
    {
        var tagDictionary = new Dictionary<string, object?>();

        foreach (var tag in tags)
        {
            tagDictionary[tag.Key] = tag.Value;
        }

        _measurements.Add(new TestMetricMeasurement<T>(measurement, tagDictionary));
    }
}

internal sealed record TestMetricMeasurement<T>(T Value, IReadOnlyDictionary<string, object?> Tags)
{
    public bool ContainsTags(string tagName) => Tags.ContainsKey(tagName);
}
