using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly ApplicationDbContext _context;

    public AttendanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ClassSection?> GetClassSectionAsync(int id, CancellationToken cancellationToken) =>
        _context.ClassSections.AsNoTracking()
            .Include(c => c.AcademicYear)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Subject?> GetSubjectAsync(int id, CancellationToken cancellationToken) =>
        _context.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<int?> GetTeacherIdByUserIdAsync(int userId, CancellationToken cancellationToken) =>
        _context.Teachers
            .Where(t => t.UserId == userId)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetUsernameAsync(int userId, CancellationToken cancellationToken) =>
        _context.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AttendanceSession?> GetSessionAsync(
        int classSectionId, DateOnly date, int? subjectId, bool forUpdate, CancellationToken cancellationToken)
    {
        IQueryable<AttendanceSession> query = _context.AttendanceSessions.Include(s => s.Records);
        if (!forUpdate) query = query.AsNoTracking();

        // EF translates the nullable comparison to "IS NULL" when subjectId is null.
        return query.FirstOrDefaultAsync(
            s => s.ClassSectionId == classSectionId && s.Date == date && s.SubjectId == subjectId,
            cancellationToken);
    }

    public Task<List<AttendanceStudentInfo>> GetRosterStudentsAsync(
        int classSectionId, IReadOnlyCollection<int> extraStudentIds, CancellationToken cancellationToken)
    {
        var extra = extraStudentIds.ToList();

        // RollNumber is a string: ordering by length first keeps "2" before "10".
        return _context.Students.AsNoTracking()
            .Where(s => (s.ClassSectionId == classSectionId && s.Status == "Active") || extra.Contains(s.Id))
            .OrderBy(s => s.RollNumber.Length)
            .ThenBy(s => s.RollNumber)
            .ThenBy(s => s.FirstName)
            .Select(s => new AttendanceStudentInfo(
                s.Id, s.RollNumber, s.FirstName, s.MiddleName, s.LastName, s.AdmNo, s.ClassSectionId, s.Status))
            .ToListAsync(cancellationToken);
    }

    public Task<AttendanceStudentInfo?> GetStudentAsync(int studentId, CancellationToken cancellationToken) =>
        _context.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new AttendanceStudentInfo(
                s.Id, s.RollNumber, s.FirstName, s.MiddleName, s.LastName, s.AdmNo, s.ClassSectionId, s.Status))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<AttendanceSessionRef>> GetRecentSessionsAsync(
        int classSectionId, DateOnly beforeDate, int? subjectId, int take, CancellationToken cancellationToken) =>
        _context.AttendanceSessions.AsNoTracking()
            .Where(s => s.ClassSectionId == classSectionId && s.SubjectId == subjectId && s.Date < beforeDate)
            .OrderByDescending(s => s.Date)
            .Take(take)
            .Select(s => new AttendanceSessionRef(s.Id, s.Date))
            .ToListAsync(cancellationToken);

    public Task<List<AttendanceStatusRow>> GetStatusRowsAsync(
        IReadOnlyCollection<int> sessionIds, CancellationToken cancellationToken)
    {
        var ids = sessionIds.ToList();
        return _context.AttendanceRecords.AsNoTracking()
            .Where(r => ids.Contains(r.SessionId))
            .Select(r => new AttendanceStatusRow(r.SessionId, r.StudentId, r.Status))
            .ToListAsync(cancellationToken);
    }

    public Task<List<StudentAttendanceRow>> GetStudentRecordsAsync(
        int studentId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        _context.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Session.Date >= from && r.Session.Date <= to)
            .OrderByDescending(r => r.Session.Date)
            .ThenBy(r => r.Session.SubjectId)
            .Select(r => new StudentAttendanceRow(
                r.Session.Date,
                r.Session.Subject == null ? null : r.Session.Subject.Name,
                r.Status,
                r.Remarks))
            .ToListAsync(cancellationToken);

    public Task<List<AttendanceCountRow>> GetDailyCountsAsync(
        int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        _context.AttendanceRecords.AsNoTracking()
            .Where(r => r.Session.ClassSectionId == classSectionId
                        && r.Session.SubjectId == null
                        && r.Session.Date >= from && r.Session.Date <= to)
            .GroupBy(r => new { r.StudentId, r.Status })
            .Select(g => new AttendanceCountRow(g.Key.StudentId, g.Key.Status, g.Count()))
            .ToListAsync(cancellationToken);

    public Task<int> CountDailySessionsAsync(
        int classSectionId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        _context.AttendanceSessions.AsNoTracking()
            .CountAsync(s => s.ClassSectionId == classSectionId
                             && s.SubjectId == null
                             && s.Date >= from && s.Date <= to, cancellationToken);

    public async Task AddSessionAsync(AttendanceSession session, CancellationToken cancellationToken)
    {
        await _context.AttendanceSessions.AddAsync(session, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One SaveChanges = one implicit transaction (session + all records together).
    /// A unique-index violation means someone else saved the same roster a moment ago.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException(
                "Attendance for this class and date was just saved by someone else. Reload and try again.");
        }
    }
}