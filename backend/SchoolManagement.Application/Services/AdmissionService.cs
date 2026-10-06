using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class AdmissionService : IAdmissionService
{
    private readonly IAdmissionRepository _admissionRepository;
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly IStudentRepository _studentRepository;

    public AdmissionService(
        IAdmissionRepository admissionRepository,
        IClassSectionRepository classSectionRepository,
        IStudentRepository studentRepository)
    {
        _admissionRepository = admissionRepository;
        _classSectionRepository = classSectionRepository;
        _studentRepository = studentRepository;
    }

    public async Task<List<AdmissionResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var admissions = await _admissionRepository.GetAllAsync(cancellationToken);
        return admissions.Select(ToResponse).ToList();
    }

    public async Task<AdmissionResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        return ToResponse(admission);
    }

    public async Task<AdmissionResponse> CreateAsync(CreateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var classSection = await GetClassSectionOrThrowAsync(request.AppliedForClassSectionId, cancellationToken);
        ClassSectionRules.EnsureActive(classSection);

        var admission = new Admission
        {
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            AcademicYearId = classSection.AcademicYearId, // derived, not caller-supplied — see design note
            AppliedForClassSectionId = classSection.Id,
            AdmissionType = request.AdmissionType,
            PreviousSchool = request.PreviousSchool,
            Phone = request.Phone,
            Email = request.Email,
            RegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "Registered",
            FatherName = request.FatherName,
            FatherMobile = request.FatherMobile,
            MotherName = request.MotherName,
            MotherMobile = request.MotherMobile,
            GuardianName = request.GuardianName,
            GuardianRelation = request.GuardianRelation,
            GuardianMobile = request.GuardianMobile,
            AddressLine = request.AddressLine,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            RegNo = "PENDING", // placeholder until Id is assigned — see below
            Grade = request.Grade,
            RegistrationFee = request.RegistrationFee,
            Notes = request.Notes,
        };

        await _admissionRepository.AddAsync(admission, cancellationToken); // Id now assigned by identity column

        // Matches the prototype exactly: RegNo = 'REG-' + year + '-' + zero-padded id,
        // not a per-year-reset counter — it's literally the row's own sequential id.
        admission.RegNo = $"REG-{DateTime.UtcNow.Year}-{admission.Id:D4}";
        await _admissionRepository.SaveChangesAsync(cancellationToken);

        admission.AcademicYear = classSection.AcademicYear;
        admission.AppliedForClassSection = classSection;
        return ToResponse(admission);
    }

    public async Task<AdmissionResponse> UpdateAsync(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        var classSection = await GetClassSectionOrThrowAsync(request.AppliedForClassSectionId, cancellationToken);

        // Only a *changed* class is checked, so editing an old applicant whose
        // class has since been deactivated still works.
        if (admission.AppliedForClassSectionId != classSection.Id)
            ClassSectionRules.EnsureActive(classSection);

        // Only registration-stage fields — Status and later-stage data
        // (fee, roll number, etc.) are never touched here, per the
        // verified business rule from the prototype's own comment.
        admission.FirstName = request.FirstName;
        admission.MiddleName = request.MiddleName;
        admission.LastName = request.LastName;
        admission.Gender = request.Gender;
        admission.DateOfBirth = request.DateOfBirth;
        admission.AppliedForClassSectionId = classSection.Id;
        admission.AcademicYearId = classSection.AcademicYearId;
        admission.AdmissionType = request.AdmissionType;
        admission.PreviousSchool = request.PreviousSchool;
        admission.Phone = request.Phone;
        admission.Email = request.Email;
        admission.FatherName = request.FatherName;
        admission.FatherMobile = request.FatherMobile;
        admission.MotherName = request.MotherName;
        admission.MotherMobile = request.MotherMobile;
        admission.GuardianName = request.GuardianName;
        admission.GuardianRelation = request.GuardianRelation;
        admission.GuardianMobile = request.GuardianMobile;
        admission.AddressLine = request.AddressLine;
        admission.City = request.City;
        admission.State = request.State;
        admission.Pincode = request.Pincode;
        admission.Grade = request.Grade;
        admission.RegistrationFee = request.RegistrationFee;
        admission.Notes = request.Notes;

        await _admissionRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(admission);
    }

    public async Task<AdmissionResponse> ConfirmAdmissionAsync(int id, ConfirmAdmissionRequest request, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        RequireStatus(admission, "Registered", "confirm admission for");

        admission.AdmissionFee = request.AdmissionFee;
        admission.AdmissionFeeReference = request.AdmissionFeeReference;
        admission.BloodGroup = request.BloodGroup;
        admission.Religion = request.Religion;
        admission.Category = request.Category;
        admission.MedicalNotes = request.MedicalNotes;
        admission.Remarks = request.Remarks;
        admission.Status = "Admitted";

        await _admissionRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(admission);
    }

    public async Task<AdmissionResponse> EnrollAsync(int id, EnrollAdmissionRequest request, CancellationToken cancellationToken)
    {
        return await _admissionRepository.ExecuteInTransactionAsync(async () =>
        {
            var admission = await GetOrThrowAsync(id, cancellationToken);
            RequireStatus(admission, "Admitted", "enroll");

            var allottedClassSection = await GetClassSectionOrThrowAsync(request.AllottedClassSectionId, cancellationToken);

            // Business rules: the section must be active and not full.
            ClassSectionRules.EnsureActive(allottedClassSection);
            ClassSectionRules.EnsureHasRoom(allottedClassSection,
                await _classSectionRepository.CountActiveStudentsAsync(allottedClassSection.Id, cancellationToken));

            admission.RollNumber = request.RollNumber;
            admission.AdmissionNumber = request.AdmissionNumber;
            admission.AdmissionDate = request.AdmissionDate;
            admission.EntryPoint = request.EntryPoint;
            admission.TransportRequired = request.TransportRequired;
            admission.AllottedClassSectionId = allottedClassSection.Id;
            admission.Status = "Enrolled";

            // Enrolling now creates the Student record in the same step —
            // no separate "Create Student Record" action. Portal login
            // credentials are still a deliberate, separate step (not set here).
            var student = new Student
            {
                AdmissionId = admission.Id,
                AdmNo = admission.AdmissionNumber!,
                RollNumber = admission.RollNumber!,
                ClassSectionId = allottedClassSection.Id,
                AdmissionDate = admission.AdmissionDate!.Value,
                Status = "Active",
                FirstName = admission.FirstName,
                MiddleName = admission.MiddleName,
                LastName = admission.LastName,
                Gender = admission.Gender,
                DateOfBirth = admission.DateOfBirth,
                BloodGroup = admission.BloodGroup,
                Nationality = request.Nationality,
                CurriculumTrack = request.CurriculumTrack,
                EnglishProficiency = request.EnglishProficiency,
                EalCode = request.EalCode,
                House = request.House,
                Allergies = request.Allergies,
            };

            await _studentRepository.AddAsync(student, cancellationToken);

            admission.StudentId = student.Id;
            await _admissionRepository.SaveChangesAsync(cancellationToken);

            admission.AllottedClassSection = allottedClassSection;
            return ToResponse(admission);
        }, cancellationToken);
    }

    public async Task<AdmissionResponse> RejectAsync(int id, RejectAdmissionRequest request, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);

        if (admission.Status is not ("Registered" or "Admitted"))
            throw new ConflictException($"Cannot reject an application with status '{admission.Status}'.");

        admission.RejectionReason = request.RejectionReason;
        admission.Status = "Rejected";

        await _admissionRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(admission);
    }

    private async Task<Admission> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _admissionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Admission {id} does not exist.");

    private async Task<ClassSection> GetClassSectionOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        // Reuses ClassSectionRepository.GetAllAsync rather than adding a
        // new GetByIdAsync there purely for this — small enough dataset
        // that this isn't a real performance concern yet.
        var sections = await _classSectionRepository.GetAllAsync(cancellationToken);
        return sections.FirstOrDefault(c => c.Id == id)
            ?? throw new NotFoundException($"Class section {id} does not exist.");
    }

    private static void RequireStatus(Admission admission, string requiredStatus, string action)
    {
        if (admission.Status != requiredStatus)
            throw new ConflictException(
                $"Cannot {action} application '{admission.RegNo}' — current status is '{admission.Status}', expected '{requiredStatus}'.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        admission.IsDeleted = true;
        admission.DeletedAt = DateTime.UtcNow;
        await _admissionRepository.SaveChangesAsync(cancellationToken);
    }

    private static AdmissionResponse ToResponse(Admission a) => new(
       a.Id, a.RegNo, a.FirstName, a.MiddleName, a.LastName, a.Gender, a.DateOfBirth,
       a.AcademicYear.Name,
       a.AppliedForClassSectionId, a.AppliedForClassSection.DisplayName, a.Grade,
       a.AdmissionType, a.PreviousSchool, a.Phone, a.Email, a.RegistrationDate,
       a.Status, a.RejectionReason,
       a.FatherName, a.FatherMobile, a.MotherName, a.MotherMobile,
       a.GuardianName, a.GuardianRelation, a.GuardianMobile,
       a.AddressLine, a.City, a.State, a.Pincode,
       a.RegistrationFee, a.Notes,
       a.AdmissionFee, a.AdmissionFeeReference, a.BloodGroup, a.Religion, a.Category,
       a.MedicalNotes, a.Remarks,
       a.RollNumber, a.AdmissionNumber, a.AdmissionDate, a.EntryPoint, a.TransportRequired,
       a.AllottedClassSectionId, a.AllottedClassSection?.DisplayName,
       a.StudentId);
}