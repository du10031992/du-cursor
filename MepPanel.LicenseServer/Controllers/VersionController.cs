using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MepPanel.LicenseServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        return Ok(new
        {
            product = "MepPanel.LicenseServer",
            version,
            environment = HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>()
                .EnvironmentName,
            utcNow = DateTime.UtcNow
        });
    }
}
