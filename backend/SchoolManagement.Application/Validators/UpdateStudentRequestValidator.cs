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
    }
}
