namespace Template.Web.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion(1.0)]
[EnableRateLimiting(LimitPolicy.Default)]
public sealed class LimitController : ControllerBase
{
    private readonly ILogger<LimitController> log;

    public LimitController(ILogger<LimitController> log)
    {
        this.log = log;
    }

    [HttpPost]
#pragma warning disable CA1848
    public async ValueTask<IActionResult> Daily()
    {
        log.LogInformation("Request start.");

        await Task.Delay(100);

        log.LogInformation("Request end.");

        return Ok();
    }
#pragma warning restore CA1848
}
