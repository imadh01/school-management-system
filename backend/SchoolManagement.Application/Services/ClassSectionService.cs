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
        return sections.Select(ToResponse).ToList();
    }

    public async Task<ClassSectionResponse> CreateAsync(CreateClassSectionRequest request, CancellationToken cancellationToken)
    {
        // Decision: an omitted AcademicYearId defaults to whichever year is
        // currently marked IsCurrent, rather than requiring the caller to
        // always pass one explicitly — matches how the prototype's own
        // CLASS_OPTIONS implicitly assumed "this year" everywhere.
        var academicYear = request.AcademicYearId.HasValue
            ? await _academicYearRepository.GetByIdAsync(request.AcademicYearId.Value, cancellationToken)
            : await _academicYearRepository.GetCurrentAsync(cancellationToken);

        if (academicYear is null)
            throw new NotFoundException(
                request.AcademicYearId.HasValue
                    ? $"Academic year {request.AcademicYearId} does not exist."
                    : "No academic year is currently marked as active.");

        if (await _classSectionRepository.ExistsAsync(academicYear.Id, request.Name, request.Section, cancellationToken))
            throw new ConflictException(
                $"'{request.Name} {request.Section}' already exists for {academicYear.Name}.");

        var classSection = new ClassSection
        {
            AcademicYearId = academicYear.Id,
            Name = request.Name,
            Section = request.Section,
            Grade = request.Grade,
            Capacity = request.Capacity,
            Room = request.Room,
            Status = "Active",
        };

        await _classSectionRepository.AddAsync(classSection, cancellationToken);

        classSection.AcademicYear = academicYear; // for DisplayName below, avoids a re-query
        return ToResponse(classSection);
    }

    private static ClassSectionResponse ToResponse(ClassSection c) => new(
        c.Id, c.Name, c.Section, c.Grade, c.Capacity, c.Room, c.Status,
        c.DisplayName, c.AcademicYearId, c.AcademicYear.Name);
}
