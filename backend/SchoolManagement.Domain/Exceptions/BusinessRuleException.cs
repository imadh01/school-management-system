namespace SchoolManagement.Domain.Exceptions;

/// <summary>The request is well-formed but breaks a business rule (HTTP 422).</summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message) : base(message) { }
}
