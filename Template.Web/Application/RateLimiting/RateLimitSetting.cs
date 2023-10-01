namespace Template.Web.Application.RateLimiting;

public class RateLimitSetting
{
    public int Window { get; set; }

    public int PermitLimit { get; set; }

    public int QueueLimit { get; set; }
}
