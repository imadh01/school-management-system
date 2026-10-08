using FluentValidation;
using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Validators;

public class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentRequestValidator()
    {
        RuleFor(x => x.RollNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ClassSectionId).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => s is "Active" or "Inactive" or "Left")
            .WithMessage("Status must be 'Active', 'Inactive', or 'Left'.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Gender).NotEmpty();
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.Category).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AdmissionType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.FeeConcessionPercent).InclusiveBetween(0m, 100m).When(x => x.FeeConcessionPercent.HasValue);

        RuleFor(x => x.Health!).SetValidator(new StudentHealthDtoValidator()).When(x => x.Health is not null);
        RuleForEach(x => x.PickupPersons).SetValidator(new PickupPersonRequestValidator());
        RuleFor(x => x.PickupPersons).Must(StudentRequestRules.PickupPersonsWithinLimit)
            .WithMessage($"A student can have at most {StudentRequestRules.MaxPickupPersons} authorised pickup persons.");
    }
}