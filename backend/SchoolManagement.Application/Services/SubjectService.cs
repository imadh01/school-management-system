using SchoolManagement.Application.DTOs.Subjects;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class SubjectService : ISubjectService
{
    private static readonly Dictionary<string, string> KnownCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mathematics"] = "MATH",
        ["English"] = "ENG",
        ["Science"] = "SCI",
        ["Computer Science"] = "CS",
        ["Art"] = "ART",
    };

    private readonly ISubjectRepository _subjectRepository;

    public SubjectService(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public async Task<List<SubjectResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var subjects = await _subjectRepository.GetAllAsync(cancellationToken);
        return subjects.Select(ToResponse).ToList();
    }

    public async Task<SubjectResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var subject = await _subjectRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Subject could not be found.");
        return ToResponse(subject);
    }

    public async Task<SubjectResponse> CreateAsync(CreateSubjectRequest request, CancellationToken cancellationToken)
    {
        var code = ResolveCode(request.Code, request.Name);

        if (await _subjectRepository.ExistsWithCodeAsync(code, request.ClassSectionId, null, cancellationToken))
            throw new ConflictException(
                "Subject Code + Class must be unique. This combination already exists.");

        var subject = new Subject
        {
            Name = request.Name,
            Code = code,
            ClassSectionId = request.ClassSectionId,
            Type = request.Type,
            Status = request.IsActive ? "Active" : "Inactive",
        };
        ApplyMarks(subject, request.Type, request.MaxMarks, request.PassMarks,
            request.TheoryMax, request.TheoryPass, request.PracticalMax, request.PracticalPass);

        await _subjectRepository.AddAsync(subject, cancellationToken);

        var created = await _subjectRepository.GetByIdAsync(subject.Id, cancellationToken);
        return ToResponse(created!);
    }

    public async Task<SubjectResponse> UpdateAsync(int id, UpdateSubjectRequest request, CancellationToken cancellationToken)
    {
        var subject = await _subjectRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Subject could not be found.");

        var code = ResolveCode(request.Code, request.Name);

        if (await _subjectRepository.ExistsWithCodeAsync(code, request.ClassSectionId, id, cancellationToken))
            throw new ConflictException(
                "Subject Code + Class must be unique. This combination already exists."
                );

        subject.Name = request.Name;
        subject.Code = code;
        subject.ClassSectionId = request.ClassSectionId;
        subject.Type = request.Type;
        subject.Status = request.IsActive ? "Active" : "Inactive";
        ApplyMarks(subject, request.Type, request.MaxMarks, request.PassMarks,
            request.TheoryMax, request.TheoryPass, request.PracticalMax, request.PracticalPass);

        await _subjectRepository.SaveChangesAsync(cancellationToken);

        var updated = await _subjectRepository.GetByIdAsync(id, cancellationToken);
        return ToResponse(updated!);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var subject = await _subjectRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Subject could not be found.");

        subject.IsDeleted = true;
        subject.DeletedAt = DateTime.UtcNow;
        await _subjectRepository.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveCode(string? code, string name)
    {
        if (!string.IsNullOrWhiteSpace(code))
            return code.Trim().ToUpperInvariant();

        if (KnownCodes.TryGetValue(name.Trim(), out var known))
            return known;

        var trimmed = name.Trim();
        return trimmed.Length <= 4
            ? trimmed.ToUpperInvariant()
            : trimmed[..4].ToUpperInvariant();
    }

    private static void ApplyMarks(
        Subject subject, string type,
        int? maxMarks, int? passMarks,
        int? theoryMax, int? theoryPass, int? practicalMax, int? practicalPass)
    {
        if (type == "Both")
        {
            subject.TheoryMax = theoryMax;
            subject.TheoryPass = theoryPass;
            subject.PracticalMax = practicalMax;
            subject.PracticalPass = practicalPass;
            subject.MaxMarks = null;
            subject.PassMarks = null;
        }
        else
        {
            subject.MaxMarks = maxMarks;
            subject.PassMarks = passMarks;
            subject.TheoryMax = null;
            subject.TheoryPass = null;
            subject.PracticalMax = null;
            subject.PracticalPass = null;
        }
    }

    private static SubjectResponse ToResponse(Subject s) => new(
     s.Id, s.Name, s.Code, s.ClassSectionId, s.ClassSection.DisplayName,
     s.Type, s.MaxMarks, s.PassMarks, s.TheoryMax, s.TheoryPass, s.PracticalMax, s.PracticalPass,
     s.Status, s.TeacherId, s.Teacher?.Name);
}
