using FluentValidation;
using SchoolManagement.Application.DTOs.Timetable;

namespace SchoolManagement.Application.Validators;

public class SaveTimetableCellsRequestValidator : AbstractValidator<SaveTimetableCellsRequest>
{
    public SaveTimetableCellsRequestValidator()
    {
        RuleFor(x => x.Cells).NotNull().WithMessage("Cells are required.")
            .Must(c => c is { Count: > 0 }).WithMessage("Send at least one cell.")
            .Must(c => c is null || c.Count <= 200).WithMessage("Send at most 200 cells at a time.");

        RuleForEach(x => x.Cells).ChildRules(cell =>
        {
            cell.RuleFor(c => c.DayOfWeek).InclusiveBetween(0, 6).WithMessage("Day must be between 0 (Sunday) and 6 (Saturday).");
            cell.RuleFor(c => c.PeriodId).GreaterThan(0).WithMessage("Period is required.");
            cell.RuleFor(c => c.SubjectId).GreaterThan(0).WithMessage("Subject is required.");
            cell.RuleFor(c => c.Room).MaximumLength(30).WithMessage("Room cannot exceed 30 characters.");
        });

        RuleFor(x => x.Cells)
            .Must(c => c is null || c.Select(x => (x.DayOfWeek, x.PeriodId)).Distinct().Count() == c.Count)
            .WithMessage("The same day and period appears more than once.");
    }
}
