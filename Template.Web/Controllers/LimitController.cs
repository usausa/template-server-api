namespace Template.Web.Controllers;

using Microsoft.AspNetCore.RateLimiting;

using Template.Web.Application;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
[EnableRateLimiting(LimitPolicy.Default)]
public class LimitController : ControllerBase
{
    private readonly ILogger<LimitController> log;

    public LimitController(ILogger<LimitController> log)
    {
        this.log = log;
    }

    [HttpPost]
    public async ValueTask<IActionResult> Daily()
    {
        log.LogInformation("Request start.");

        await Task.Delay(100);

        log.LogInformation("Request end.");

        return Ok();
    }
}
