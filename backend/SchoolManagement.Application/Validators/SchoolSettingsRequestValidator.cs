using FluentValidation;
using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.Services;

namespace SchoolManagement.Application.Validators;

public class UpdateSchoolSettingsRequestValidator : AbstractValidator<UpdateSchoolSettingsRequest>
{
    private static readonly string[] DayNames = Enum.GetNames<DayOfWeek>();

    public UpdateSchoolSettingsRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.SchoolName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ShortName).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(100).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).Must(UserRequestRules.IsOptionalPhone).WithMessage(PhoneNumberRules.Message);
        RuleFor(x => x.Website).MaximumLength(200)
            .Must(BeHttpUrl).WithMessage("Website must be a full address starting with http:// or https://.")
            .When(x => !string.IsNullOrWhiteSpace(x.Website));
        RuleFor(x => x.AddressText).MaximumLength(300);
        RuleFor(x => x.CountryId).GreaterThan(0).When(x => x.CountryId is not null);
        RuleFor(x => x.TimeZoneId).MaximumLength(64)
            .Must(SchoolTimeZones.IsValidIanaId).WithMessage(SchoolTimeZones.Message);
        RuleFor(x => x.CurrencyCode).NotEmpty().Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a three-letter ISO 4217 code in capitals, for example AED or INR.");
        RuleFor(x => x.WorkingDays).NotEmpty().WithMessage("Choose at least one working day.");
        RuleForEach(x => x.WorkingDays).Must(d => DayNames.Contains(d))
            .WithMessage("'{PropertyValue}' is not a day name (Monday ... Sunday).");
        RuleFor(x => x.WorkingDays).Must(d => d is null || d.Distinct().Count() == d.Count)
            .WithMessage("A working day is listed twice.");
        RuleFor(x => x.AboutTagline).MaximumLength(300);
    }

    private static bool BeHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
