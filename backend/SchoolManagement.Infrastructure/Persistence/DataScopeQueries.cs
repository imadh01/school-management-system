using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// THE definition of "records related to this user" (Batch F1), as composable queries.
///
/// Every method returns an IQueryable built from subqueries, so the filter runs inside the same SQL
/// statement as the read: lists, counts and lookups by id all use the same rule, and nothing is
/// loaded into memory or cached between requests (an assignment change applies on the next request).
///
/// Only READS call these. Enrolment, saving attendance and the other internal operations must see
/// every row, which is why this is not an EF global query filter.
///
/// Relationships (see DataScope): teacher of the current class this academic year, guardian, self.
/// Deleted teachers, parents and students drop out through their soft-delete query filters.
/// </summary>
public static class DataScopeQueries
{
    // ------------------------------------------------------------------ building blocks

    /// <summary>The Teacher rows of this user (normally zero or one).</summary>
    private static IQueryable<int> TeacherIdsOf(ApplicationDbContext db, int userId) =>
        db.Teachers.Where(t => t.UserId == userId).Select(t => t.Id);

    /// <summary>Sections of the CURRENT academic year where the user is the class teacher.</summary>
    public static IQueryable<int> ClassTeacherSectionIds(ApplicationDbContext db, int userId)
    {
        var teacherIds = TeacherIdsOf(db, userId);
        return db.ClassSections
            .Where(c => c.AcademicYear.IsCurrent
                        && c.ClassTeacherId != null
                        && teacherIds.Contains(c.ClassTeacherId.Value))
            .Select(c => c.Id);
    }

    /// <summary>Subjects of CURRENT-year sections that the user teaches.</summary>
    public static IQueryable<int> TaughtSubjectIds(ApplicationDbContext db, int userId)
    {
        var teacherIds = TeacherIdsOf(db, userId);
        return db.Subjects
            .Where(s => s.ClassSection.AcademicYear.IsCurrent
                        && s.TeacherId != null
                        && teacherIds.Contains(s.TeacherId.Value))
            .Select(s => s.Id);
    }

    /// <summary>Sections of the CURRENT academic year the user teaches: as class teacher or subject teacher.</summary>
    public static IQueryable<int> TaughtSectionIds(ApplicationDbContext db, int userId)
    {
        var teacherIds = TeacherIdsOf(db, userId);
        return db.ClassSections
            .Where(c => c.AcademicYear.IsCurrent
                        && ((c.ClassTeacherId != null && teacherIds.Contains(c.ClassTeacherId.Value))
                            || db.Subjects.Any(s => s.ClassSectionId == c.Id
                                                    && s.TeacherId != null
                                                    && teacherIds.Contains(s.TeacherId.Value))))
            .Select(c => c.Id);
    }

    /// <summary>Students the user is a guardian of (through Parent.UserId).</summary>
    public static IQueryable<int> ChildIds(ApplicationDbContext db, int userId) =>
        db.StudentGuardians.Where(g => g.Parent.UserId == userId).Select(g => g.StudentId);

    // ------------------------------------------------------------------ students

    /// <summary>Students visible under <paramref name="scope"/>: taught this year, own children, or self.</summary>
    public static IQueryable<Student> VisibleTo(this IQueryable<Student> students, ApplicationDbContext db, DataScope scope)
    {
        if (scope.IsUnrestricted) return students;
        var userId = scope.UserId!.Value;

        var taughtSections = TaughtSectionIds(db, userId);
        var children = ChildIds(db, userId);
        return students.Where(s => taughtSections.Contains(s.ClassSectionId)
                                   || children.Contains(s.Id)
                                   || s.UserId == userId);
    }

    // ------------------------------------------------------------------ parents

    /// <summary>
    /// Parents visible under <paramref name="scope"/>: the user's OWN parent record, and the parents of
    /// students the user teaches (or of the user, when the user is a student).
    /// Deliberately NOT the co-guardians of the user's own children: a parent sees only themself.
    /// </summary>
    public static IQueryable<Parent> VisibleTo(this IQueryable<Parent> parents, ApplicationDbContext db, DataScope scope)
    {
        if (scope.IsUnrestricted) return parents;
        var userId = scope.UserId!.Value;

        var taughtSections = TaughtSectionIds(db, userId);
        var reachableStudents = db.Students
            .Where(s => taughtSections.Contains(s.ClassSectionId) || s.UserId == userId)
            .Select(s => s.Id);
        var guardiansOfReachable = db.StudentGuardians
            .Where(g => reachableStudents.Contains(g.StudentId))
            .Select(g => g.ParentId);

        return parents.Where(p => p.UserId == userId || guardiansOfReachable.Contains(p.Id));
    }

    // ------------------------------------------------------------------ attendance

    /// <summary>
    /// Attendance records visible under <paramref name="scope"/>. "Student in scope" is not enough: a
    /// subject teacher must not see the student's other subjects. So:
    ///   - own child / self: every record;
    ///   - class teacher (current year): every session of that class, daily and subject-wise;
    ///   - subject teacher (current year): only the sessions of that subject.
    /// </summary>
    public static IQueryable<AttendanceRecord> VisibleTo(
        this IQueryable<AttendanceRecord> records, ApplicationDbContext db, DataScope scope)
    {
        if (scope.IsUnrestricted) return records;
        var userId = scope.UserId!.Value;

        var children = ChildIds(db, userId);
        var classTeacherSections = ClassTeacherSectionIds(db, userId);
        var taughtSubjects = TaughtSubjectIds(db, userId);

        return records.Where(r => children.Contains(r.StudentId)
                                  || r.Student.UserId == userId
                                  || classTeacherSections.Contains(r.Session.ClassSectionId)
                                  || (r.Session.SubjectId != null && taughtSubjects.Contains(r.Session.SubjectId.Value)));
    }

    /// <summary>
    /// May the caller open the roster/summary of this class (and subject)? The same session rule as
    /// above: class teacher → every session of the class; subject teacher → that subject only.
    /// </summary>
    public static IQueryable<bool> CanSeeSessions(
        ApplicationDbContext db, DataScope scope, int classSectionId, int? subjectId)
    {
        if (scope.IsUnrestricted)
            return db.ClassSections.Where(c => c.Id == classSectionId).Select(_ => true);

        var userId = scope.UserId!.Value;
        var classTeacherSections = ClassTeacherSectionIds(db, userId);
        var taughtSubjects = TaughtSubjectIds(db, userId);

        // Every sub-expression references a query variable (c, s): a condition built only from the
        // method's parameters would be evaluated by EF before the query runs, as a separate query.
        return db.ClassSections
            .Where(c => c.Id == classSectionId)
            .Select(c => classTeacherSections.Contains(c.Id)
                         || db.Subjects.Any(s => s.Id == subjectId
                                                 && s.ClassSectionId == c.Id
                                                 && taughtSubjects.Contains(s.Id)));
    }
}
