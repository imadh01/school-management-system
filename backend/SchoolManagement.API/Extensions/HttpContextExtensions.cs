namespace SchoolManagement.API.Extensions;

public static class HttpContextExtensions
{
    private const string CorrelationIdKey = "CorrelationId";

    public static string GetCorrelationId(this HttpContext context) =>
        context.Items[CorrelationIdKey] as string ?? context.TraceIdentifier;
}
