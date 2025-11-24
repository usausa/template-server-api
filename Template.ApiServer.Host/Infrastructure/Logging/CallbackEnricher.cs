namespace Template.ApiServer.Host.Infrastructure.Logging;

using Serilog.Core;
using Serilog.Events;

// TODO
public sealed class CallbackEnricher : ILogEventEnricher
{
    private readonly string name;

    private readonly Func<object?> resolver;

    public CallbackEnricher(string name, Func<object?> resolver)
    {
        this.name = name;
        this.resolver = resolver;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var enrichProperty = propertyFactory.CreateProperty(name, resolver());
        logEvent.AddOrUpdateProperty(enrichProperty);
    }
}
