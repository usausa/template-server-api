namespace Template.ApiServer.Host.Application.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public sealed class ApplicationInstrument : IDisposable
{
    private readonly Meter meter;

    public ActivitySource ActivitySource { get; }

    private readonly Counter<long> requestLongExecution;

    private readonly Counter<long> requestExecution;

    public ApplicationInstrument(IMeterFactory meterFactory)
    {
        ActivitySource = new ActivitySource(Source.Name, Source.Version);
        meter = meterFactory.Create(Source.Name, Source.Version);

        meter.CreateObservableCounter("application.uptime", ObserveApplicationUptime);

        requestLongExecution = meter.CreateCounter<long>("api.request.long.execution", description: "Long execution count.");
        requestExecution = meter.CreateCounter<long>("api.request.execution", description: "API request count");
    }

    public void Dispose()
    {
        meter.Dispose();
    }

    private static long ObserveApplicationUptime() =>
        (long)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;

    public void IncrementRequestLongExecution() => requestLongExecution.Add(1);

    public void IncrementRequestExecution(string area, string controller, string action) =>
        requestExecution.Add(1, new("area", area), new("controller", controller), new("action", action));
}
