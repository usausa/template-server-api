namespace Template.Web.Application.RateLimiting;

public sealed class RateLimitSetting
{
    public int Window { get; set; }

    public int PermitLimit { get; set; }

    public int QueueLimit { get; set; }
}
