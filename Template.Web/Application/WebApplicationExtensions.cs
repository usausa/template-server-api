namespace Template.Web.Application;

public static class WebApplicationExtensions
{
#pragma warning disable IDE0060
    public static ValueTask InitializeAsync(this WebApplication app)
    {
        return ValueTask.CompletedTask;
    }
#pragma warning restore IDE0060
}
