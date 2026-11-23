using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudentIncidentSystem.ViewModels;

namespace StudentIncidentSystem.Services
{
    // Records every form submission, plus a few sensitive page views.
    // It never records passwords, message text, or note contents.
    public class AuditActionFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> AuditedGets = new(StringComparer.OrdinalIgnoreCase)
        {
            "StaffIncidents.Details", "Attachments.Download", "Reports.ExportCsv", "Reports.Print"
        };

        private readonly AuditService _audit;

        public AuditActionFilter(AuditService audit)
        {
            _audit = audit;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
            var key = controllerName + "." + actionName;

            var isPost = HttpMethods.IsPost(context.HttpContext.Request.Method);
            var isAuditedGet = HttpMethods.IsGet(context.HttpContext.Request.Method) && AuditedGets.Contains(key);

            if (!isPost && !isAuditedGet)
            {
                await next();
                return;
            }

            var before = context.HttpContext.User; // captured now, because logout clears it
            var executed = await next();

            try
            {
                await WriteAsync(context, executed, before, key, controllerName, isPost);
            }
            catch
            {
                // auditing must never break a request
            }
        }

        private async Task WriteAsync(ActionExecutingContext context, ActionExecutedContext executed,
            ClaimsPrincipal before, string key, string controllerName, bool isPost)
        {
            var http = context.HttpContext;
            var principal = before.Identity?.IsAuthenticated == true ? before : http.User;
            var authenticated = principal.Identity?.IsAuthenticated == true;

            var userId = authenticated ? principal.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            var userName = authenticated ? principal.Identity!.Name : null;

            // Only the email is taken from login and registration forms. Never the password.
            string? attemptedEmail = null;
            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg is LoginViewModel login) attemptedEmail = login.Email;
                else if (arg is RegisterViewModel reg) attemptedEmail = reg.Email;
            }
            userName ??= attemptedEmail;

            var tempData = (context.Controller as Controller)?.TempData;
            var success = tempData?.Peek("Success") as string;
            var error = tempData?.Peek("Error") as string;
            var result = executed.Result;

            string outcome;
            if (executed.Exception != null && !executed.ExceptionHandled) outcome = "Error";
            else if (result is NotFoundResult or NotFoundObjectResult) outcome = "NotFound";
            else if (result is ForbidResult or ChallengeResult) outcome = "Denied";
            else if (isPost && (error != null || result is ViewResult)) outcome = "Failed";
            else outcome = "Success";

            var parts = new List<string> { outcome };
            if (attemptedEmail != null && !authenticated) parts.Add("email: " + attemptedEmail);
            if (!string.IsNullOrWhiteSpace(success)) parts.Add(success);
            if (!string.IsNullOrWhiteSpace(error)) parts.Add(error);

            await _audit.LogAsync(
                userId, userName, key, controllerName,
                context.RouteData.Values["id"]?.ToString(),
                string.Join(" | ", parts),
                http.Connection.RemoteIpAddress?.ToString());
        }
    }
}
