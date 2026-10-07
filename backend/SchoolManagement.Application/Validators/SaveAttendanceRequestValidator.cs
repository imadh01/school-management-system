using FluentValidation;
using SchoolManagement.Application.DTOs.Attendance;
using SchoolManagement.Application.Services;

namespace SchoolManagement.Application.Validators;

public class SaveAttendanceRequestValidator : AbstractValidator<SaveAttendanceRequest>
{
    public SaveAttendanceRequestValidator()
    {
        RuleFor(x => x.ClassSectionId).GreaterThan(0).WithMessage("Class is required.");
        RuleFor(x => x.Date).Must(d => d != default).WithMessage("Date is required.");
        RuleFor(x => x.SubjectId).GreaterThan(0)
            .When(x => x.SubjectId.HasValue)
            .WithMessage("Subject is invalid.");

        RuleFor(x => x.Records).NotEmpty().WithMessage("At least one student record is required.");

        RuleFor(x => x.Records)
            .Must(r => r.Select(e => e.StudentId).Distinct().Count() == r.Count)
            .When(x => x.Records is not null)
            .WithMessage("A student appears more than once.");

        RuleForEach(x => x.Records).ChildRules(entry =>
        {
            entry.RuleFor(e => e.StudentId).GreaterThan(0);
            entry.RuleFor(e => e.Status)
                .Must(s => AttendanceRules.Statuses.Contains(s))
                .WithMessage("Status must be Present, Absent, Late, Half Day or Leave.");
            entry.RuleFor(e => e.Remarks).MaximumLength(250);
        });
    }
}