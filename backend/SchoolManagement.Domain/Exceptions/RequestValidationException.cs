namespace SchoolManagement.Domain.Exceptions;

/// <summary>
/// Field errors found by a service rather than by a FluentValidation validator, because the check
/// needs data (e.g. "this postcode does not fit the chosen country"). Answered with the same 400
/// VALIDATION_ERROR body as validator errors, so the form shows them under the right fields.
/// Keys use the validator style: "Addresses[0].PostalCode".
/// </summary>
public class RequestValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Validation failed.")
    {
        Errors = errors;
    }
}
