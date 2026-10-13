using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.API.Filters;

/// <summary>
/// G1b: sizes the request-body limit of an upload endpoint from the Documents:MaxFileSizeBytes
/// SETTING (an attribute like [RequestSizeLimit] would need the number in the code).
///
/// Runs before model binding, so the body is not read yet:
///   - a request whose Content-Length is clearly over the limit is answered at once, without
///     reading the body (the browser checks the size first, so this is for other clients);
///   - otherwise the server limit is set to the file limit plus <see cref="FormAllowance"/>. A file
///     that is a little over the limit is therefore still received, and DocumentService answers with
///     its EXACT size.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DocumentUploadLimitAttribute : Attribute, IAsyncResourceFilter
{
    /// <summary>Room for the multipart boundaries and the other form fields.</summary>
    public const long FormAllowance = 1024 * 1024;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var settings = context.HttpContext.RequestServices.GetRequiredService<DocumentSettings>();
        var bodyLimit = settings.MaxFileSizeBytes + FormAllowance;

        var contentLength = context.HttpContext.Request.ContentLength;
        if (contentLength > bodyLimit)
        {
            var message =
                $"The upload is {FileSignatures.DescribeSize(contentLength.Value)} (file and form together). " +
                $"The largest file allowed is {FileSignatures.DescribeSize(settings.MaxFileSizeBytes)}.";
            context.Result = new ObjectResult(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = new Dictionary<string, string[]> { ["File"] = new[] { message } },
                TraceId = context.HttpContext.GetCorrelationId(),
            })
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge,
            };
            return;
        }

        // Kestrel's own limit (30 MB by default) for THIS request; a chunked body without a
        // Content-Length that goes over it ends in 413 (GlobalExceptionMiddleware).
        var sizeFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = bodyLimit;

        await next();
    }
}
