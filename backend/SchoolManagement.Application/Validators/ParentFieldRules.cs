using System.Linq.Expressions;
using FluentValidation;

namespace SchoolManagement.Application.Validators;

/// <summary>
/// The widths of the Parents columns (ParentConfiguration). The parent validators use these so a value that is too
/// long is a 400 naming the field, not a database error. ParentFieldLimitsTests checks them against the EF model.
/// </summary>
public static class ParentFieldLimits
{
    public const int Name = 100;
    public const int Email = 100;          // Email and WorkEmail
    public const int Phone = 20;           // Mobile, Whatsapp and WorkPhone
    public const int Occupation = 100;
    public const int Nationality = 50;
    public const int CountryOfResidence = 50;
    public const int Timezone = 50;
    public const int PreferredLanguage = 30;
    public const int PreferredContactMethod = 30;
    public const int Employer = 100;
    public const int JobTitle = 100;
}

/// <summary>The field rules shared by the create and the edit parent requests.</summary>
public static class ParentFieldRules
{
    /// <param name="mobile">Required.</param>
    /// <param name="email">Optional, an email address.</param>
    /// <param name="whatsapp">Optional, a phone number.</param>
    /// <param name="workEmail">Optional, an email address.</param>
    /// <param name="workPhone">Optional, a phone number.</param>
    public static void ValidateParentFields<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, string>> mobile,
        Expression<Func<T, string?>> email,
        Expression<Func<T, string?>> occupation,
        Expression<Func<T, string?>> nationality,
        Expression<Func<T, string?>> countryOfResidence,
        Expression<Func<T, string?>> timezone,
        Expression<Func<T, string?>> preferredLanguage,
        Expression<Func<T, string?>> preferredContactMethod,
        Expression<Func<T, string?>> whatsapp,
        Expression<Func<T, string?>> employer,
        Expression<Func<T, string?>> jobTitle,
        Expression<Func<T, string?>> workEmail,
        Expression<Func<T, string?>> workPhone)
    {
        validator.RuleFor(name).NotEmpty().MaximumLength(ParentFieldLimits.Name);

        // The length is checked on the raw value: the regex trims, the column does not.
        validator.RuleFor(mobile).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(ParentFieldLimits.Phone)
            .Must(PhoneNumberRules.IsValid).WithMessage(PhoneNumberRules.Message);

        foreach (var optionalEmail in new[] { email, workEmail })
            validator.RuleFor(optionalEmail).MaximumLength(ParentFieldLimits.Email)
                .EmailAddress().When(IsGiven(optionalEmail));

        foreach (var optionalPhone in new[] { whatsapp, workPhone })
            validator.RuleFor(optionalPhone).MaximumLength(ParentFieldLimits.Phone)
                .Must(UserRequestRules.IsOptionalPhone).WithMessage(PhoneNumberRules.Message);

        validator.RuleFor(occupation).MaximumLength(ParentFieldLimits.Occupation);
        validator.RuleFor(nationality).MaximumLength(ParentFieldLimits.Nationality);
        validator.RuleFor(countryOfResidence).MaximumLength(ParentFieldLimits.CountryOfResidence);
        validator.RuleFor(timezone).MaximumLength(ParentFieldLimits.Timezone);
        validator.RuleFor(preferredLanguage).MaximumLength(ParentFieldLimits.PreferredLanguage);
        validator.RuleFor(preferredContactMethod).MaximumLength(ParentFieldLimits.PreferredContactMethod);
        validator.RuleFor(employer).MaximumLength(ParentFieldLimits.Employer);
        validator.RuleFor(jobTitle).MaximumLength(ParentFieldLimits.JobTitle);
    }

    private static Func<T, bool> IsGiven<T>(Expression<Func<T, string?>> property)
    {
        var read = property.Compile();
        return x => !string.IsNullOrEmpty(read(x));
    }
}
