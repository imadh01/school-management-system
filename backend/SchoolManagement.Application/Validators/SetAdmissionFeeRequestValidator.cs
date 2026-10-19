using FluentValidation;
using SchoolManagement.Application.DTOs.AdmissionFees;

namespace SchoolManagement.Application.Validators;

public class SetAdmissionFeeRequestValidator : AbstractValidator<SetAdmissionFeeRequest>
{
    public SetAdmissionFeeRequestValidator()
    {
        RuleFor(x => x.Amount)
            .InclusiveBetween(0m, 1_000_000m)
            .WithMessage("The fee must be between 0 and 1,000,000.");
    }
}
