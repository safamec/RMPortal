using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RMPortal.Services;

[AllowAnonymous] // remove if you want it protected
public class DevController : Controller
{
    private readonly IEmailService _email;
    public DevController(IEmailService email) => _email = email;

    // Absolute attribute route:
    // GET /dev/test-email?to=someone@example.com
    [HttpGet("/dev/test-email")]
    public async Task<IActionResult> TestEmail(string to)
    {
        if (string.IsNullOrWhiteSpace(to)) return BadRequest("Provide ?to=email@domain");
        var res = await _email.SendAsync(to, "RMPortal test", "<p>It works ✅</p>");
        return Ok(res.Succeeded ? "Sent" : $"Failed: {res.Error}");
    }
}
