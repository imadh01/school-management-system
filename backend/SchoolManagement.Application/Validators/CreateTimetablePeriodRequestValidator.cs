using FluentValidation;
using SchoolManagement.Application.DTOs.Timetable;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

public class CreateTimetablePeriodRequestValidator : AbstractValidator<CreateTimetablePeriodRequest>
{
    public CreateTimetablePeriodRequestValidator()
    {
        RuleFor(x => x.AcademicYearId).GreaterThan(0).WithMessage("Academic year is required.");
        TimetablePeriodRules.Apply(this, x => x.Label, x => x.StartTime, x => x.EndTime, x => x.Kind, x => x.SortOrder);
    }
}

public class UpdateTimetablePeriodRequestValidator : AbstractValidator<UpdateTimetablePeriodRequest>
{
    public UpdateTimetablePeriodRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        TimetablePeriodRules.Apply(this, x => x.Label, x => x.StartTime, x => x.EndTime, x => x.Kind, x => x.SortOrder);
    }
}

/// <summary>The rules create and update share, written once.</summary>
internal static class TimetablePeriodRules
{
    public static void Apply<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> label,
        System.Linq.Expressions.Expression<Func<T, string>> start,
        System.Linq.Expressions.Expression<Func<T, string>> end,
        System.Linq.Expressions.Expression<Func<T, string>> kind,
        System.Linq.Expressions.Expression<Func<T, int>> sortOrder)
    {
        var getStart = start.Compile();
        var getEnd = end.Compile();

        validator.RuleFor(label).NotEmpty().WithMessage("Period name is required.")
            .MaximumLength(50).WithMessage("Period name cannot exceed 50 characters.");
        validator.RuleFor(start)
            .Must(v => TimetableRules.TryParseTime(v, out _)).WithMessage("Start time must be a time like 08:00.");
        validator.RuleFor(end)
            .Must(v => TimetableRules.TryParseTime(v, out _)).WithMessage("End time must be a time like 08:45.");
        validator.RuleFor(end)
            .Must((model, value) =>
                !TimetableRules.TryParseTime(getStart(model), out var s)
                || !TimetableRules.TryParseTime(value, out var e)
                || s < e)
            .WithMessage("End time must be after the start time.");
        validator.RuleFor(kind).Must(PeriodKinds.IsValid).WithMessage("Type must be Teaching or Break.");
        validator.RuleFor(sortOrder).InclusiveBetween(1, 30).WithMessage("Order must be between 1 and 30.");
    }
}
