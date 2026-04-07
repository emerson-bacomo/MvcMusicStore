using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace MvcMusic.Middlewares
{
    public class RequirePasswordChangeMiddleware
    {
        private readonly RequestDelegate _next;

        public RequirePasswordChangeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var requiresPasswordChangeClaim = context.User.FindFirst("RequiresPasswordChange");

                if (requiresPasswordChangeClaim != null && requiresPasswordChangeClaim.Value == "true")
                {
                    var path = context.Request.Path.ToString().ToLower();

                    // Allow certain endpoints so we don't get into an infinite loop or break styling
                    var isAllowedEndpoints = 
                        path == "/account/change-temporary-password" || 
                        path == "/account/logout" ||
                        path.StartsWith("/css/") ||
                        path.StartsWith("/js/") ||
                        path.StartsWith("/lib/") ||
                        path.StartsWith("/images/");

                    if (!isAllowedEndpoints)
                    {
                        context.Response.Redirect("/account/change-temporary-password?forced=true");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
