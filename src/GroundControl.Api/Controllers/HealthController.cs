using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "ok" });
    }

    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(new { version = "1.0.0", service = "ground" });
    }
}