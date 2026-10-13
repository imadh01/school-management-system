using Microsoft.Extensions.Options;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.API.Configuration;

/// <summary>G1b: checked at startup (ValidateOnStart), so a bad "Documents" setting stops the API with its reason.</summary>
public sealed class DocumentSettingsValidator : IValidateOptions<DocumentSettings>
{
    public ValidateOptionsResult Validate(string? name, DocumentSettings options)
    {
        var problems = options.Problems().ToList();
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}
