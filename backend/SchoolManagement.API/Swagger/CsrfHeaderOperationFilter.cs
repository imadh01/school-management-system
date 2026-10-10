using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using SchoolManagement.API.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SchoolManagement.API.Swagger;

/// <summary>
/// Shows the X-Requested-With header (pre-filled with the expected value) on every endpoint that
/// has [RequireCsrfHeader], so /api/auth/refresh and /api/auth/logout can be tried from Swagger UI.
/// </summary>
public sealed class CsrfHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var required =
            context.MethodInfo.IsDefined(typeof(RequireCsrfHeaderAttribute), inherit: true) ||
            (context.MethodInfo.DeclaringType?.IsDefined(typeof(RequireCsrfHeaderAttribute), inherit: true) ?? false);

        if (!required)
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = RequireCsrfHeaderAttribute.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "CSRF protection for cookie-authenticated endpoints.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new OpenApiString(RequireCsrfHeaderAttribute.ExpectedValue),
            },
        });
    }
}
