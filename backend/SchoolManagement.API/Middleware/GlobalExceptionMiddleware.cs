using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var correlationId = context.GetCorrelationId();

        var (statusCode, errorCode, message) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND", ex.Message),
            ConflictException => (StatusCodes.Status409Conflict, "CONFLICT", ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected error occurred."),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            // Full exception logged server-side only — never sent to the client.
            _logger.LogError(ex, "Unhandled exception for request {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogWarning("{ErrorCode}: {Message} for request {Path}", errorCode, message, context.Request.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ApiErrorResponse
        {
            Message = message,
            ErrorCode = errorCode,
            TraceId = correlationId,
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
