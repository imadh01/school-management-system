using FluentValidation;
using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Validators;

public class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.AdmNo).NotEmpty().MaximumLength(30);
        RuleFor(x => x.RollNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ClassSectionId).GreaterThan(0);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Gender).NotEmpty();
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
