using SchoolManagement.Application.DTOs.ClassSections;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class ClassSectionService : IClassSectionService
{
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly IAcademicYearRepository _academicYearRepository;

    public ClassSectionService(
        IClassSectionRepository classSectionRepository,
        IAcademicYearRepository academicYearRepository)
    {
        _classSectionRepository = classSectionRepository;
        _academicYearRepository = academicYearRepository;
    }

    public async Task<List<ClassSectionResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var sections = await _classSectionRepository.GetAllAsync(cancellationToken);
        var counts = await _classSectionRepository.GetEnrolledCountsAsync(cancellationToken);
        return sections.Select(c => ToResponse(c, counts.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<ClassSectionResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var section = await GetOrThrowAsync(id, cancellationToken);
        var enrolled = await _classSectionRepository.CountActiveStudentsAsync(id, cancellationToken);
        return ToResponse(section, enrolled);
    }

    public async Task<ClassSectionResponse> CreateAsync(CreateClassSectionRequest request, CancellationToken cancellationToken)
    {
        // An omitted AcademicYearId defaults to the year currently marked IsCurrent.
        var academicYear = request.AcademicYearId.HasValue
            ? await _academicYearRepository.GetByIdAsync(request.AcademicYearId.Value, cancellationToken)
            : await _academicYearRepository.GetCurrentAsync(cancellationToken);

        if (academicYear is null)
            throw new NotFoundException(
                request.AcademicYearId.HasValue
                    ? $"Academic year {request.AcademicYearId} does not exist."
                    : "No academic year is currently marked as active.");

        var name = request.Name.Trim();
        var sectionLetter = request.Section.Trim();

        if (await _classSectionRepository.ExistsAsync(academicYear.Id, name, sectionLetter, null, cancellationToken))
            throw new ConflictException($"'{name} {sectionLetter}' already exists for {academicYear.Name}.");

        var classSection = new ClassSection
        {
            AcademicYearId = academicYear.Id,
            Name = name,
            Section = sectionLetter,
            Grade = request.Grade,
            Stage = request.Stage,
            Medium = request.Medium,
            Stream = request.Stream,
            Capacity = request.Capacity,
            Building = Clean(request.Building),
            Floor = request.Floor,
            Room = Clean(request.Room),
            Status = request.IsActive ? "Active" : "Inactive",
        };

        await _classSectionRepository.AddAsync(classSection, cancellationToken);

        classSection.AcademicYear = academicYear; // for DisplayName, avoids a re-query
        return ToResponse(classSection, 0);
    }

    public async Task<ClassSectionResponse> UpdateAsync(int id, UpdateClassSectionRequest request, CancellationToken cancellationToken)
    {
        var section = await GetOrThrowAsync(id, cancellationToken);

        var name = request.Name.Trim();
        var sectionLetter = request.Section.Trim();

        if (await _classSectionRepository.ExistsAsync(section.AcademicYearId, name, sectionLetter, id, cancellationToken))
            throw new ConflictException($"'{name} {sectionLetter}' already exists for {section.AcademicYear.Name}.");

        var enrolled = await _classSectionRepository.CountActiveStudentsAsync(id, cancellationToken);
        if (request.Capacity < enrolled)
            throw new ConflictException(
                $"Capacity cannot be lower than the current strength ({enrolled} students).");

        section.Name = name;
        section.Section = sectionLetter;
        section.Grade = request.Grade;
        section.Stage = request.Stage;
        section.Medium = request.Medium;
        section.Stream = request.Stream;
        section.Capacity = request.Capacity;
        section.Building = Clean(request.Building);
        section.Floor = request.Floor;
        section.Room = Clean(request.Room);
        section.Status = request.IsActive ? "Active" : "Inactive";

        await _classSectionRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(section, enrolled);
    }

    public async Task<ClassSectionResponse> ChangeStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var section = await GetOrThrowAsync(id, cancellationToken);
        section.Status = isActive ? "Active" : "Inactive";
        await _classSectionRepository.SaveChangesAsync(cancellationToken);

        var enrolled = await _classSectionRepository.CountActiveStudentsAsync(id, cancellationToken);
        return ToResponse(section, enrolled);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var section = await GetOrThrowAsync(id, cancellationToken);

        var deps = await _classSectionRepository.GetDependenciesAsync(id, cancellationToken);
        if (deps.Any)
        {
            var parts = new List<string>();
            if (deps.Students > 0) parts.Add($"{deps.Students} student(s)");
            if (deps.Admissions > 0) parts.Add($"{deps.Admissions} admission(s)");
            if (deps.Subjects > 0) parts.Add($"{deps.Subjects} subject(s)");

            throw new ConflictException(
                $"Cannot delete '{section.DisplayName}' — it still has {string.Join(", ", parts)}. Mark it Inactive instead.");
        }

        section.IsDeleted = true;
        section.DeletedAt = DateTime.UtcNow;
        await _classSectionRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<ClassSection> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _classSectionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Class section {id} does not exist.");

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClassSectionResponse ToResponse(ClassSection c, int enrolled) => new(
        c.Id, c.Name, c.Section, c.Code, c.Grade, c.Stage, c.Medium, c.Stream,
        c.Capacity, enrolled, c.Building, c.Floor, c.Room, c.Status,
        c.DisplayName, c.AcademicYearId, c.AcademicYear.Name);
}