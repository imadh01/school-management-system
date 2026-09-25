using SchoolManagement.Application.DTOs.Parents;
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
        var links = await _guardianRepository.GetForStudentAsync(studentId, cancellationToken);
        return links.Select(ToResponse).ToList();
    }

    public async Task<StudentGuardianResponse> LinkAsync(int studentId, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        var parent = await _parentRepository.GetByIdAsync(request.ParentId, cancellationToken)
            ?? throw new NotFoundException($"Parent {request.ParentId} does not exist.");

        if (await _guardianRepository.LinkExistsAsync(studentId, request.ParentId, cancellationToken))
            throw new ConflictException($"Parent {request.ParentId} is already linked to student {studentId}.");

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

    public async Task<List<LinkedStudentResponse>> GetForParentAsync(int parentId, CancellationToken cancellationToken)
    {
        var links = await _guardianRepository.GetForParentAsync(parentId, cancellationToken);
        return links.Select(ToLinkedStudentResponse).ToList();
    }

    public async Task<LinkedStudentResponse> LinkStudentAsync(int parentId, LinkStudentRequest request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.StudentId, cancellationToken)
            ?? throw new NotFoundException($"Student {request.StudentId} does not exist.");

        if (await _guardianRepository.LinkExistsAsync(request.StudentId, parentId, cancellationToken))
            throw new ConflictException($"Student {request.StudentId} is already linked to parent {parentId}.");

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

    public Task SetPrimaryAsync(int parentId, int studentId, CancellationToken cancellationToken) =>
        _guardianRepository.SetPrimaryAsync(studentId, parentId, cancellationToken);

    private static StudentGuardianResponse ToResponse(StudentGuardian sg) => new(
        sg.ParentId, sg.Parent.Name, sg.Parent.Mobile, sg.Parent.Email,
        sg.RelationType, sg.IsPrimaryContact);

    private static LinkedStudentResponse ToLinkedStudentResponse(StudentGuardian sg) => new(
        sg.StudentId, $"{sg.Student.FirstName} {sg.Student.LastName}", sg.Student.AdmNo,
        sg.Student.ClassSection.DisplayName, sg.Student.RollNumber,
        sg.RelationType, sg.IsPrimaryContact);
}
