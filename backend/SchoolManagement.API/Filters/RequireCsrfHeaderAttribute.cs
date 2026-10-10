using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;

namespace SchoolManagement.API.Filters;

/// <summary>
/// Second line of CSRF defence for endpoints that authenticate with a COOKIE (refresh, logout).
///
/// The first line is SameSite=Strict: the browser does not send the cookie on requests started by
/// another site. This attribute additionally requires a custom header. A cross-site form or image
/// cannot add custom headers at all, and a cross-site fetch() that adds one triggers a CORS
/// preflight that our CORS policy rejects. So a request carrying the header came from our own
/// frontend (or a non-browser client, which has no victim cookies to abuse).
///
/// Bearer-token endpoints do not need this: browsers never attach the Authorization header on their own.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireCsrfHeaderAttribute : ActionFilterAttribute
{
    public const string HeaderName = "X-Requested-With";
    public const string ExpectedValue = "eschool";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var value = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.Equals(value, ExpectedValue, StringComparison.Ordinal))
            return;

        context.Result = new ObjectResult(new ApiErrorResponse
        {
            Message = "This request is missing a required header.",
            ErrorCode = "CSRF_CHECK_FAILED",
            TraceId = context.HttpContext.GetCorrelationId(),
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
