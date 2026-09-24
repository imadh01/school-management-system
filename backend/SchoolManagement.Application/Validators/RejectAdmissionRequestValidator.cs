using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Validators;

public class RejectAdmissionRequestValidator : AbstractValidator<RejectAdmissionRequest>
{
    public RejectAdmissionRequestValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(300);
    }
}
