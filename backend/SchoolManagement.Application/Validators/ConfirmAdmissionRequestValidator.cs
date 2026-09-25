using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Validators;

public class ConfirmAdmissionRequestValidator : AbstractValidator<ConfirmAdmissionRequest>
{
    public ConfirmAdmissionRequestValidator()
    {
        RuleFor(x => x.AdmissionFee).GreaterThanOrEqualTo(0).When(x => x.AdmissionFee.HasValue);
    }
}
