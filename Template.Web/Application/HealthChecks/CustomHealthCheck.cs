namespace Template.Web.Application.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
