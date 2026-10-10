using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

public class ConfirmAdmissionRequestValidator : AbstractValidator<ConfirmAdmissionRequest>
{
    public ConfirmAdmissionRequestValidator()
    {
        // Column is decimal(10,2): 99,999,999.99 is the largest value that fits.
        RuleFor(x => x.AdmissionFee).InclusiveBetween(0m, 99_999_999.99m).When(x => x.AdmissionFee.HasValue);
        RuleFor(x => x.AdmissionFeeReference).MaximumLength(30);
        RuleFor(x => x.BloodGroup).MaximumLength(10);
        RuleFor(x => x.Religion).MaximumLength(50);
        RuleFor(x => x.Category).MaximumLength(FieldLimits.Category);
        RuleFor(x => x.MedicalNotes).MaximumLength(300);
        RuleFor(x => x.Remarks).MaximumLength(300);
    }
}
