using Serilog.Context;
using SchoolManagement.API.Extensions;

namespace SchoolManagement.API.Middleware;

/// <summary>
/// Reads an incoming X-Correlation-Id header if the caller supplied one
/// (useful when this API is called by another service), otherwise
/// generates a new one. Pushes it into Serilog's LogContext so every
/// log line for this request carries it automatically, and echoes it
/// back on the response header so a caller can find their own logs.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
