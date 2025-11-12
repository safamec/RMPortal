using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RMPortal.Services;

namespace RMPortal.Controllers
{
    public class AccountController : Controller
    {
        private readonly IFakeAdService _ad;

        public AccountController(IFakeAdService ad) => _ad = ad;

        // GET /Account/Login?returnUrl=/Requests/Create
        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            var users = _ad.GetAllUsers();   // alice, bob, dave, safa...
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

            // ===== Build claims =====
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, u.DisplayName),
                new Claim(ClaimTypes.NameIdentifier, u.Sam),
                new Claim("sam", u.Sam),
                new Claim(ClaimTypes.Email, u.Email ?? string.Empty)
            };

            foreach (var g in _ad.GetGroupsForUser(u.Sam))
                claims.Add(new Claim("groups", g));  // لسياسات IsManager/IsIT

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            // ===== Hard redirects by user (override any returnUrl) =====
            var lowerSam = (u.Sam ?? "").Trim().ToLowerInvariant();

            if (lowerSam == "safa" || lowerSam == "safaa" || lowerSam == "safa.ahmed")
            {
                // Safaa → Requests/Create
                return RedirectToAction("Create", "Requests");
            }

            if (lowerSam == "bob")
            {
                // Bob → Manager dashboard
                return RedirectToAction("Index", "Manager");
            }

            if (lowerSam == "dave" || lowerSam == "dava")
            {
                // Dave → Dashboard
                return RedirectToAction("Index", "Dashboard");
            }

            // ===== Otherwise: honor returnUrl if local =====
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // ===== Role-based default landing =====
            if (principal.HasClaim("groups", "RM_LineManagers"))
                return RedirectToAction("Index", "Manager");

            if (principal.HasClaim("groups", "RM_ITAdmins"))
                return RedirectToAction("Index", "IT");

            // ===== Fallback =====
            return RedirectToAction("Index", "Dashboard");
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
}
