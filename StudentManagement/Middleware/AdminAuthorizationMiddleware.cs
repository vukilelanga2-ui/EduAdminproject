using System.Security.Claims;

namespace StudentManagement.Middleware
{
    public class AdminAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminAuthorizationMiddleware> _logger;

        private readonly string[] _publicPaths = {
            "/Account/Login",
            "/Account/Logout",
            "/Account/AccessDenied",
            "/signin-google",
            "/signin-github",
            "/Home/Error",
            "/favicon.ico"
        };

        public AdminAuthorizationMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<AdminAuthorizationMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            // Allow public paths and static files
            if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Check if user is authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            // Check if user is an admin
            var userEmail = context.User.FindFirstValue(ClaimTypes.Email) ?? "";
            var adminEmails = _configuration.GetSection("AdminEmails").Get<string[]>() ?? Array.Empty<string>();

            var isAdmin = adminEmails.Any(e => e.Equals(userEmail, StringComparison.OrdinalIgnoreCase))
                          || context.User.FindFirstValue("IsAdmin") == "true";

            if (!isAdmin)
            {
                _logger.LogWarning("Unauthorized access attempt by {Email}", userEmail);
                context.Response.Redirect("/Account/AccessDenied");
                return;
            }

            await _next(context);
        }
    }
}
