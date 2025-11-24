namespace Template.ApiServer.Host.Application.Telemetry;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

public static class MeterProviderBuilderExtensions
{
    public static MeterProviderBuilder AddApplicationInstrumentation(this MeterProviderBuilder builder)
    {
        builder.AddMeter(Source.Name);
        return builder;
    }

    public static TracerProviderBuilder AddApplicationInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource(Source.Name);
        return builder;
    }

    public static IServiceCollection AddApplicationInstrument(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationInstrument>();
        return services;
    }
}
