using FluentValidation;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

public static class UserRequestRules
{
    public static IRuleBuilderOptions<T, string> MustBeValidUsername<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username can only contain letters, digits, dots, underscores, and hyphens.");

    public const string GenderMessage = "Gender must be 'Male' or 'Female'.";

    public static bool IsPastDate(DateOnly? date) =>
        date is null || date.Value < DateOnly.FromDateTime(DateTime.UtcNow);

    public static bool IsOptionalPhone(string? value) =>
        string.IsNullOrWhiteSpace(value) || PhoneNumberRules.IsValid(value);
}

/// <summary>
/// The Users form (create and edit). Full name, gender, mobile and joining date are required here; rows
/// copied in by the E2a migration may lack them until someone edits the user.
/// </summary>
public class StaffProfileRequestValidator : AbstractValidator<StaffProfileRequest>
{
    public StaffProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Gender).NotEmpty().Must(Genders.IsValid).WithMessage(UserRequestRules.GenderMessage);
        RuleFor(x => x.DateOfBirth).Must(UserRequestRules.IsPastDate).WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Mobile).NotEmpty().Must(PhoneNumberRules.IsValid).WithMessage(PhoneNumberRules.Message);
        RuleFor(x => x.JoiningDate).NotNull().WithMessage("Joining date is required.");
        RuleFor(x => x.Qualification).MaximumLength(150);
        RuleFor(x => x.EmployeeNumber).MaximumLength(30);
        RuleFor(x => x.BloodGroup).MaximumLength(10);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).Must(UserRequestRules.IsOptionalPhone).WithMessage(PhoneNumberRules.Message);
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.Username).MustBeValidUsername();
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.RoleName).NotEmpty();
        RuleFor(x => x.Profile).NotNull().SetValidator(new StaffProfileRequestValidator());
        this.ValidateAddresses(x => x.Addresses);
    }
}

public class ChangeUserStatusRequestValidator : AbstractValidator<ChangeUserStatusRequest>
{
    public ChangeUserStatusRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.Status).Must(UserStatuses.IsValid)
            .WithMessage("Status must be Active, Inactive or Suspended.");
    }
}

public class ResetUserPasswordRequestValidator : AbstractValidator<ResetUserPasswordRequest>
{
    public ResetUserPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MustBeStrongPassword();
    }
}

public class UpdateMyProfileRequestValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Gender).NotEmpty().Must(Genders.IsValid).WithMessage(UserRequestRules.GenderMessage);
        RuleFor(x => x.DateOfBirth).Must(UserRequestRules.IsPastDate).WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Mobile).NotEmpty().Must(PhoneNumberRules.IsValid).WithMessage(PhoneNumberRules.Message);
        RuleFor(x => x.Qualification).MaximumLength(150);
        RuleFor(x => x.BloodGroup).MaximumLength(10);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactPhone).Must(UserRequestRules.IsOptionalPhone).WithMessage(PhoneNumberRules.Message);
        this.ValidateAddresses(x => x.Addresses);
    }
}
