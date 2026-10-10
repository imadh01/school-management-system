using FluentValidation;
using SchoolManagement.Application.DTOs.Roles;

namespace SchoolManagement.Application.Validators;

/// <summary>Shape rules only. Business rules (system names, dependencies, escalation) live in RoleService.</summary>
internal static class RoleRules
{
    public const int NameMaxLength = 30;        // Roles.Name column
    public const int DescriptionMaxLength = 200; // Roles.Description column

    /// <summary>Letters, digits, spaces and hyphens, starting with a letter: "Exam Cell", "Vice-Principal".</summary>
    public const string NamePattern = @"^[A-Za-z][A-Za-z0-9 \-]*$";

    public static void Name<T>(IRuleBuilderInitial<T, string> rule) =>
        rule.NotEmpty().WithMessage("Name is required.")
            .MaximumLength(NameMaxLength)
            .Matches(NamePattern).WithMessage("Name may contain only letters, digits, spaces and hyphens, and must start with a letter.");
}

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RoleRules.Name(RuleFor(x => x.Name));
        RuleFor(x => x.Description).MaximumLength(RoleRules.DescriptionMaxLength);
        RuleFor(x => x.Permissions).NotNull().WithMessage("Permissions are required (an empty list is allowed).");
    }
}

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RoleRules.Name(RuleFor(x => x.Name));
        RuleFor(x => x.Description).MaximumLength(RoleRules.DescriptionMaxLength);
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
    }
}

public class SetRolePermissionsRequestValidator : AbstractValidator<SetRolePermissionsRequest>
{
    public SetRolePermissionsRequestValidator()
    {
        RuleFor(x => x.Permissions).NotNull().WithMessage("Permissions are required (an empty list is allowed).");
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
    }
}

public class ChangeRoleStatusRequestValidator : AbstractValidator<ChangeRoleStatusRequest>
{
    public ChangeRoleStatusRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
    }
}
