namespace Template.ApiServer.Host.Application.Telemetry;

using System.Reflection;

public static class Source
{
    private static readonly AssemblyName AssemblyName = typeof(Source).Assembly.GetName();

    public static string Name => AssemblyName.Name!;

    public static string Version => AssemblyName.Version!.ToString();
}
