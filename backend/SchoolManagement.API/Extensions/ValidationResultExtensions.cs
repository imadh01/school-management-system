using FluentValidation.Results;

namespace SchoolManagement.API.Extensions;

public static class ValidationResultExtensions
{
    public static IDictionary<string, string[]> ToErrorDictionary(this ValidationResult result) =>
        result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}