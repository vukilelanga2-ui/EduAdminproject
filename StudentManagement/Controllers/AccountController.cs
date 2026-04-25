using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace StudentManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IConfiguration configuration, ILogger<AccountController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpGet]
        public IActionResult LoginWithGoogle(string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account",
                new { returnUrl }, Request.Scheme);

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                Items = { { "LoginProvider", "Google" } }
            };

            return Challenge(properties, "Google");
        }

        [HttpGet]
        public IActionResult LoginWithGitHub(string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account",
                new { returnUrl }, Request.Scheme);

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                Items = { { "LoginProvider", "GitHub" } }
            };

            return Challenge(properties, "GitHub");
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                TempData["Error"] = "Authentication failed. Please try again.";
                _logger.LogWarning("External authentication failed");
                return RedirectToAction("Login");
            }

            var email = result.Principal?.FindFirstValue(ClaimTypes.Email) ?? "";
            var name = result.Principal?.FindFirstValue(ClaimTypes.Name) ?? "";

            string provider = "Unknown";
            if (result.Properties?.Items != null && result.Properties.Items.TryGetValue("LoginProvider", out var p) && p != null)
            {
                provider = p;
            }

            // Check if admin
            var adminEmails = _configuration.GetSection("AdminEmails").Get<string[]>() ?? Array.Empty<string>();
            var isAdmin = adminEmails.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (!isAdmin)
            {
                _logger.LogWarning("Non-admin login attempt: {Email} via {Provider}", email, provider);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["Error"] = $"Access denied. {email} is not an authorised administrator.";
                return RedirectToAction("Login");
            }

            // Build admin claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, name),
                new Claim("IsAdmin", "true"),
                new Claim("LoginProvider", provider),
                new Claim(ClaimTypes.NameIdentifier, email)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _logger.LogInformation("Admin {Email} logged in via {Provider}", email, provider);
            TempData["Success"] = $"Welcome back, {name}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var name = User.FindFirstValue(ClaimTypes.Name);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync();
            HttpContext.Session.Clear();
            _logger.LogInformation("Admin {Name} logged out", name);
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
