using FluentValidation;
using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Validators;

public class CreateParentRequestValidator : AbstractValidator<CreateParentRequest>
{
    public CreateParentRequestValidator()
    {
        this.ValidateParentFields(
            x => x.Name, x => x.Mobile, x => x.Email,
            x => x.Occupation, x => x.Nationality, x => x.CountryOfResidence, x => x.Timezone,
            x => x.PreferredLanguage, x => x.PreferredContactMethod, x => x.Whatsapp,
            x => x.Employer, x => x.JobTitle, x => x.WorkEmail, x => x.WorkPhone);
        this.ValidateAddresses(x => x.Addresses);
    }
}
