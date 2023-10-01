namespace Template.Web.Application.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

using Template.Web.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRateLimiter(this IServiceCollection services, RateLimitSetting setting)
    {
        services.AddRateLimiter(config =>
        {
            config.AddFixedWindowLimiter(LimitPolicy.Default, options =>
            {
                options.Window = TimeSpan.FromMilliseconds(setting.Window);
                options.PermitLimit = setting.PermitLimit;
                options.QueueLimit = setting.QueueLimit;
            });
            config.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
