using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class StudentGuardianService : IStudentGuardianService
{
    private readonly IStudentGuardianRepository _guardianRepository;
    private readonly IParentRepository _parentRepository;
    private readonly IStudentRepository _studentRepository;

    public StudentGuardianService(
        IStudentGuardianRepository guardianRepository,
        IParentRepository parentRepository,
        IStudentRepository studentRepository)
    {
        _guardianRepository = guardianRepository;
        _parentRepository = parentRepository;
        _studentRepository = studentRepository;
    }

    public async Task<List<StudentGuardianResponse>> GetForStudentAsync(int studentId, CancellationToken cancellationToken)
    {
        // F1: an unknown student is a 404 now (it used to be an empty list), like every other student read.
        await GetStudentAccessOrThrowAsync(studentId, DataScope.Unrestricted, cancellationToken);
        var links = await _guardianRepository.GetForStudentAsync(studentId, cancellationToken);
        return links.Select(ToResponse).ToList();
    }

    public async Task<List<GuardianContactResponse>> GetContactsForStudentAsync(
        int studentId, DataScope scope, CancellationToken cancellationToken)
    {
        var access = await GetStudentAccessOrThrowAsync(studentId, scope, cancellationToken);
        var links = await _guardianRepository.GetForStudentAsync(studentId, cancellationToken);
        return StudentGuardianRules.VisibleLinks(links, access, scope)
            .OrderByDescending(l => l.IsPrimaryContact).ThenBy(l => l.Parent.Name)
            .Select(StudentGuardianRules.ToContact)
            .ToList();
    }

    public async Task<StudentGuardianResponse> LinkAsync(int studentId, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        var parent = await _parentRepository.GetByIdAsync(request.ParentId, cancellationToken)
            ?? throw new NotFoundException($"Parent {request.ParentId} does not exist.");

        // 404 for an unknown student (it used to fail later on the foreign key and answer 500).
        await GetStudentAccessOrThrowAsync(studentId, DataScope.Unrestricted, cancellationToken);

        if (await _guardianRepository.LinkExistsAsync(studentId, request.ParentId, cancellationToken))
            throw new ConflictException($"Parent {request.ParentId} is already linked to student {studentId}.");

        await EnsureNoOtherPrimaryAsync(studentId, request.IsPrimaryContact, cancellationToken);

        var link = new StudentGuardian
        {
            StudentId = studentId,
            ParentId = request.ParentId,
            RelationType = request.RelationType,
            IsPrimaryContact = request.IsPrimaryContact,
        };

        await _guardianRepository.AddLinkAsync(link, cancellationToken);
        link.Parent = parent;
        return ToResponse(link);
    }

    public async Task UnlinkAsync(int studentId, int parentId, CancellationToken cancellationToken)
    {
        var link = await _guardianRepository.GetLinkAsync(studentId, parentId, cancellationToken)
            ?? throw new NotFoundException($"No guardian link exists between student {studentId} and parent {parentId}.");

        await _guardianRepository.RemoveLinkAsync(link, cancellationToken);
    }

    public async Task<List<LinkedStudentResponse>> GetForParentAsync(int parentId, DataScope scope, CancellationToken cancellationToken)
    {
        // F1: an unknown OR out-of-scope parent is the same 404 (it used to be an empty list).
        _ = await _parentRepository.GetVisibleByIdAsync(parentId, scope, cancellationToken)
            ?? throw new NotFoundException($"Parent {parentId} does not exist.");

        var links = await _guardianRepository.GetForParentAsync(parentId, scope, cancellationToken);
        return links.Select(ToLinkedStudentResponse).ToList();
    }

    public async Task<LinkedStudentResponse> LinkStudentAsync(int parentId, LinkStudentRequest request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.StudentId, cancellationToken)
            ?? throw new NotFoundException($"Student {request.StudentId} does not exist.");

        // 404 for an unknown parent (it used to fail later on the foreign key and answer 500).
        _ = await _parentRepository.GetByIdAsync(parentId, cancellationToken)
            ?? throw new NotFoundException($"Parent {parentId} does not exist.");

        if (await _guardianRepository.LinkExistsAsync(request.StudentId, parentId, cancellationToken))
            throw new ConflictException($"Student {request.StudentId} is already linked to parent {parentId}.");

        await EnsureNoOtherPrimaryAsync(request.StudentId, request.IsPrimaryContact, cancellationToken);

        var link = new StudentGuardian
        {
            StudentId = request.StudentId,
            ParentId = parentId,
            RelationType = request.RelationType,
            IsPrimaryContact = request.IsPrimaryContact,
        };

        await _guardianRepository.AddLinkAsync(link, cancellationToken);
        link.Student = student;
        return ToLinkedStudentResponse(link);
    }

    public async Task SetPrimaryAsync(int parentId, int studentId, CancellationToken cancellationToken)
    {
        if (!await _guardianRepository.SetPrimaryAsync(studentId, parentId, cancellationToken))
            throw new NotFoundException($"No guardian link exists between student {studentId} and parent {parentId}.");
    }

    /// <summary>
    /// A student has at most one primary contact (filtered unique index). Linking a second one as primary is a
    /// 409 that names the current primary, instead of a database error.
    /// </summary>
    private async Task EnsureNoOtherPrimaryAsync(int studentId, bool isPrimary, CancellationToken cancellationToken)
    {
        if (!isPrimary) return;

        var links = await _guardianRepository.GetForStudentAsync(studentId, cancellationToken);
        var current = links.FirstOrDefault(l => l.IsPrimaryContact);
        if (current is not null)
            throw new ConflictException(
                $"Student {studentId} already has a primary contact ({current.Parent.Name}). " +
                "Link this guardian without the primary flag, then use \"Set as primary\" to switch.");
    }

    private async Task<StudentAccess> GetStudentAccessOrThrowAsync(int studentId, DataScope scope, CancellationToken cancellationToken)
    {
        var access = await _studentRepository.GetAccessAsync(studentId, scope, cancellationToken);
        return access == StudentAccess.None
            ? throw new NotFoundException($"Student {studentId} does not exist.")
            : access;
    }

    private static StudentGuardianResponse ToResponse(StudentGuardian sg) => new(
        sg.ParentId, sg.Parent.Name, sg.Parent.Mobile, sg.Parent.Email,
        sg.RelationType, sg.IsPrimaryContact);

    private static LinkedStudentResponse ToLinkedStudentResponse(StudentGuardian sg) => new(
        sg.StudentId, $"{sg.Student.FirstName} {sg.Student.LastName}", sg.Student.AdmNo,
        sg.Student.ClassSection.DisplayName, sg.Student.RollNumber,
        sg.RelationType, sg.IsPrimaryContact);
}
