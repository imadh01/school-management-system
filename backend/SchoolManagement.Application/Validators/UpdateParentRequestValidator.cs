using FluentValidation;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

public class UpdateParentRequestValidator : AbstractValidator<UpdateParentRequest>
{
    public UpdateParentRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        this.ValidateParentFields(
            x => x.Name, x => x.Mobile, x => x.Email,
            x => x.Occupation, x => x.Nationality, x => x.CountryOfResidence, x => x.Timezone,
            x => x.PreferredLanguage, x => x.PreferredContactMethod, x => x.Whatsapp,
            x => x.Employer, x => x.JobTitle, x => x.WorkEmail, x => x.WorkPhone);
        this.ValidateAddresses(x => x.Addresses);
        RuleFor(x => x.Status).Must(ActiveStatuses.IsValid)
            .WithMessage("Status must be 'Active' or 'Inactive'.");
    }
}
