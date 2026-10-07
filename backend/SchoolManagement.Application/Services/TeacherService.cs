using SchoolManagement.Application.DTOs.Teachers;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class TeacherService : ITeacherService
{
    private readonly ITeacherRepository _teachers;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _passwordHasher;

    public TeacherService(
        ITeacherRepository teachers,
        IRoleRepository roles,
        IPasswordHasher passwordHasher)
    {
        _teachers = teachers;
        _roles = roles;
        _passwordHasher = passwordHasher;
    }

    // ---------- Teacher CRUD ----------

    public async Task<List<TeacherResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var teachers = await _teachers.GetAllAsync(cancellationToken);
        var stats = await _teachers.GetStatsAsync(cancellationToken);
        return teachers.Select(t => ToResponse(t, stats.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TeacherResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var teacher = await GetOrThrowAsync(id, cancellationToken);
        return ToResponse(teacher, await GetStatsForAsync(id, cancellationToken));
    }

    public async Task<TeacherResponse> CreateAsync(CreateTeacherRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _teachers.UsernameExistsAsync(username, null, cancellationToken))
            throw new ConflictException($"Username '{username}' is already taken.");
        if (await _teachers.EmailExistsAsync(email, null, cancellationToken))
            throw new ConflictException($"Email '{email}' is already registered.");

        var role = await _roles.GetByNameAsync("Teacher", cancellationToken)
            ?? throw new NotFoundException("Role 'Teacher' does not exist.");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Status = request.Status,
        };
        user.UserRoles.Add(new UserRole { Role = role, AssignedAt = now });

        var teacher = new Teacher
        {
            User = user,
            Name = request.Name.Trim(),
            Phone = Clean(request.Phone),
            Specialization = Clean(request.Specialization),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _teachers.AddAsync(teacher, cancellationToken);
        return ToResponse(teacher, null);
    }

    public async Task<TeacherResponse> UpdateAsync(int id, UpdateTeacherRequest request, CancellationToken cancellationToken)
    {
        var teacher = await GetOrThrowAsync(id, cancellationToken);
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _teachers.UsernameExistsAsync(username, teacher.UserId, cancellationToken))
            throw new ConflictException($"Username '{username}' is already taken.");
        if (await _teachers.EmailExistsAsync(email, teacher.UserId, cancellationToken))
            throw new ConflictException($"Email '{email}' is already registered.");

        teacher.Name = request.Name.Trim();
        teacher.Phone = Clean(request.Phone);
        teacher.Specialization = Clean(request.Specialization);
        teacher.UpdatedAt = DateTime.UtcNow;

        teacher.User.Username = username;
        teacher.User.Email = email;
        teacher.User.Status = request.Status;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            teacher.User.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);

        await _teachers.SaveChangesAsync(cancellationToken);
        return ToResponse(teacher, await GetStatsForAsync(id, cancellationToken));
    }

    public async Task<TeacherResponse> ChangeStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        var teacher = await GetOrThrowAsync(id, cancellationToken);
        teacher.User.Status = status;
        teacher.UpdatedAt = DateTime.UtcNow;
        await _teachers.SaveChangesAsync(cancellationToken);
        return ToResponse(teacher, await GetStatsForAsync(id, cancellationToken));
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var teacher = await GetOrThrowAsync(id, cancellationToken);

        var stats = await GetStatsForAsync(id, cancellationToken);
        if (stats is not null && (stats.Subjects > 0 || stats.ClassTeacherOf > 0))
            throw new ConflictException(
                $"Cannot delete '{teacher.Name}' — still assigned to {stats.Subjects} subject(s) " +
                $"and class teacher of {stats.ClassTeacherOf} class(es). Remove the assignments first.");

        var now = DateTime.UtcNow;
        teacher.IsDeleted = true;
        teacher.DeletedAt = now;
        // The login goes with the profile.
        teacher.User.IsDeleted = true;
        teacher.User.DeletedAt = now;

        await _teachers.SaveChangesAsync(cancellationToken);
    }

    // ---------- Assignments ----------

    public async Task<List<TeacherAssignmentResponse>> GetAssignmentsAsync(int id, CancellationToken cancellationToken)
    {
        await GetOrThrowAsync(id, cancellationToken);
        return await BuildAssignmentsAsync(id, cancellationToken);
    }

    public async Task<List<TeacherAssignmentResponse>> AssignSubjectsAsync(
        int id, AssignSubjectsRequest request, CancellationToken cancellationToken)
    {
        var teacher = await GetOrThrowAsync(id, cancellationToken);
        var section = await GetSectionOrThrowAsync(request.ClassSectionId, cancellationToken);

        var ids = request.SubjectIds.Distinct().ToList();
        var subjects = await _teachers.GetSubjectsByIdsAsync(ids, cancellationToken);
        if (subjects.Count != ids.Count)
            throw new NotFoundException("One or more subjects do not exist.");

        var wrongClass = subjects.Where(s => s.ClassSectionId != section.Id).ToList();
        if (wrongClass.Count > 0)
            throw new ConflictException(
                $"These subjects do not belong to {section.DisplayName}: {string.Join(", ", wrongClass.Select(s => s.Name))}.");

        // Decision 2: a subject can have only one teacher — block, don't silently reassign.
        var taken = subjects.Where(s => s.TeacherId is not null && s.TeacherId != id).ToList();
        if (taken.Count > 0)
            throw new ConflictException(
                "Already taught by another teacher: " +
                string.Join("; ", taken.Select(s => $"{s.Name} ({s.Teacher?.Name ?? "another teacher"})")) + ".");

        if (request.MakeClassTeacher)
            EnsureClassTeacherFree(section, id);

        foreach (var subject in subjects)
            subject.TeacherId = id;
        if (request.MakeClassTeacher)
            section.ClassTeacherId = id;

        teacher.UpdatedAt = DateTime.UtcNow;
        await _teachers.SaveChangesAsync(cancellationToken);
        return await BuildAssignmentsAsync(id, cancellationToken);
    }

    public async Task<List<TeacherAssignmentResponse>> UnassignSubjectAsync(
        int id, int subjectId, CancellationToken cancellationToken)
    {
        await GetOrThrowAsync(id, cancellationToken);

        var subject = await _teachers.GetSubjectAsync(subjectId, cancellationToken)
            ?? throw new NotFoundException($"Subject {subjectId} does not exist.");
        if (subject.TeacherId != id)
            throw new NotFoundException("This subject is not assigned to this teacher.");

        // Mirrors the prototype: removing a teacher's last subject in a class
        // also drops their class-teacher role for that class.
        var inClass = await _teachers.CountSubjectsInClassAsync(id, subject.ClassSectionId, cancellationToken);
        if (inClass <= 1)
        {
            var section = await _teachers.GetClassSectionAsync(subject.ClassSectionId, cancellationToken);
            if (section is not null && section.ClassTeacherId == id)
                section.ClassTeacherId = null;
        }

        subject.TeacherId = null;
        await _teachers.SaveChangesAsync(cancellationToken);
        return await BuildAssignmentsAsync(id, cancellationToken);
    }

    public async Task<List<TeacherAssignmentResponse>> SetClassTeacherAsync(
        int id, int classSectionId, CancellationToken cancellationToken)
    {
        await GetOrThrowAsync(id, cancellationToken);
        var section = await GetSectionOrThrowAsync(classSectionId, cancellationToken);

        // The class-teacher flag lives on an assignment in the prototype,
        // so the teacher must already teach something in this class.
        if (await _teachers.CountSubjectsInClassAsync(id, classSectionId, cancellationToken) == 0)
            throw new ConflictException(
                $"Assign at least one subject in {section.DisplayName} before making this teacher its class teacher.");

        EnsureClassTeacherFree(section, id);
        section.ClassTeacherId = id;

        await _teachers.SaveChangesAsync(cancellationToken);
        return await BuildAssignmentsAsync(id, cancellationToken);
    }

    public async Task<List<TeacherAssignmentResponse>> ClearClassTeacherAsync(
        int id, int classSectionId, CancellationToken cancellationToken)
    {
        await GetOrThrowAsync(id, cancellationToken);
        var section = await GetSectionOrThrowAsync(classSectionId, cancellationToken);

        if (section.ClassTeacherId != id)
            throw new ConflictException($"This teacher is not the class teacher of {section.DisplayName}.");

        section.ClassTeacherId = null;
        await _teachers.SaveChangesAsync(cancellationToken);
        return await BuildAssignmentsAsync(id, cancellationToken);
    }

    // ---------- helpers ----------

    private async Task<Teacher> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _teachers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Teacher {id} does not exist.");

    private async Task<ClassSection> GetSectionOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _teachers.GetClassSectionAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Class section {id} does not exist.");

    private async Task<TeacherStats?> GetStatsForAsync(int id, CancellationToken cancellationToken) =>
        (await _teachers.GetStatsAsync(cancellationToken)).GetValueOrDefault(id);

    private static void EnsureClassTeacherFree(ClassSection section, int teacherId)
    {
        if (section.ClassTeacherId is not null && section.ClassTeacherId != teacherId)
            throw new ConflictException(
                $"{section.DisplayName} already has a class teacher ({section.ClassTeacher?.Name ?? "another teacher"}). " +
                "Remove them as class teacher first.");
    }

    private async Task<List<TeacherAssignmentResponse>> BuildAssignmentsAsync(int teacherId, CancellationToken cancellationToken)
    {
        var subjects = await _teachers.GetAssignedSubjectsAsync(teacherId, cancellationToken);
        var classTeacherOf = (await _teachers.GetClassTeacherSectionIdsAsync(teacherId, cancellationToken)).ToHashSet();

        return subjects
            .GroupBy(s => s.ClassSection)
            .OrderByDescending(g => g.Key.AcademicYear.StartDate)
            .ThenBy(g => g.Key.Grade ?? 0)
            .ThenBy(g => g.Key.Name)
            .ThenBy(g => g.Key.Section)
            .Select(g => new TeacherAssignmentResponse(
                g.Key.Id,
                g.Key.DisplayName,
                g.Key.AcademicYear.Name,
                classTeacherOf.Contains(g.Key.Id),
                g.OrderBy(s => s.Name)
                    .Select(s => new TeacherSubjectResponse(s.Id, s.Name, s.Code))
                    .ToList()))
            .ToList();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TeacherResponse ToResponse(Teacher t, TeacherStats? s) => new(
        t.Id, t.UserId, t.Name, t.User.Username, t.User.Email, t.Phone, t.Specialization,
        t.User.Status, s?.Classes ?? 0, s?.Subjects ?? 0, s?.ClassTeacherOf ?? 0);
}
