using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class StudentGuardianService : IStudentGuardianService
{
    private readonly IStudentGuardianRepository _guardianRepository;
    private readonly IParentRepository _parentRepository;

    public StudentGuardianService(
        IStudentGuardianRepository guardianRepository,
        IParentRepository parentRepository)
    {
        _guardianRepository = guardianRepository;
        _parentRepository = parentRepository;
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

    private static StudentGuardianResponse ToResponse(StudentGuardian sg) => new(
        sg.ParentId, sg.Parent.Name, sg.Parent.Mobile, sg.Parent.Email,
        sg.RelationType, sg.IsPrimaryContact);
}
