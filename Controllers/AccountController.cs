using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RMPortal.Services; // <-- required
// adjust to your namespace

public class AccountController : Controller
{
    private readonly IFakeAdService _ad;

    public AccountController(IFakeAdService ad) => _ad = ad;

    // GET /Account/Login?ReturnUrl=/Requests/Create
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        var users = _ad.GetAllUsers();   // alice, bob, carol, dave...
        return View(users);
    }

    // POST /Account/Login
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string sam, string? returnUrl = null)
    {
        var u = _ad.GetUser(sam);
        if (u is null)
        {
            ModelState.AddModelError("", "User not found.");
            ViewBag.ReturnUrl = returnUrl;
            return View(_ad.GetAllUsers());
        }

        // Build claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, u.DisplayName),
            new Claim("sam", u.Sam),
            new Claim(ClaimTypes.Email, u.Email)
        };

        // Add role claims from fake AD groups (RM_LineManagers, RM_Security, RM_ITAdmins...)
        foreach (var g in _ad.GetGroupsForUser(u.Sam))
            claims.Add(new Claim(ClaimTypes.Role, g));

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // validate returnUrl is local
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
