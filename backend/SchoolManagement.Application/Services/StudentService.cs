using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly IStudentGuardianRepository _guardianRepository;

    public StudentService(
        IStudentRepository studentRepository,
        IClassSectionRepository classSectionRepository,
        IStudentGuardianRepository guardianRepository)
    {
        _studentRepository = studentRepository;
        _classSectionRepository = classSectionRepository;
        _guardianRepository = guardianRepository;
    }

    public async Task<List<StudentSummaryResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await _studentRepository.GetSummariesAsync(cancellationToken);
        return rows.Select(r => new StudentSummaryResponse(
            r.Id, r.AdmNo, r.RollNumber, r.ClassSectionId,
            ClassSection.BuildDisplayName(r.ClassName, r.ClassSection, r.AcademicYearName),
            r.AdmissionDate, r.Status, r.PhotoUrl,
            r.FirstName, r.MiddleName, r.LastName, r.Gender, r.DateOfBirth,
            r.Mobile, r.Category, r.TransportRequired,
            r.Nationality, r.CurriculumTrack, r.House, r.EalCode,
            r.Allergies, r.AdmissionRegNo, r.AdmissionId)).ToList();
    }

    public async Task<StudentResponse> GetByIdAsync(int id, bool includeSensitive, CancellationToken cancellationToken)
    {
        var student = await GetDetailOrThrowAsync(id, cancellationToken);
        return await ToResponseAsync(student, includeSensitive, cancellationToken);
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, bool includeSensitive, CancellationToken cancellationToken)
    {
        if (await _studentRepository.ExistsByAdmNoAsync(request.AdmNo, cancellationToken))
            throw new ConflictException($"AdmNo '{request.AdmNo}' is already in use.");

        if (await _studentRepository.ExistsByRollNumberInClassAsync(request.ClassSectionId, request.RollNumber, cancellationToken))
            throw new ConflictException($"Roll number '{request.RollNumber}' is already in use in this class.");

        var classSection = await GetClassSectionOrThrowAsync(request.ClassSectionId, cancellationToken);

        // Business rules: the section must be active and not full.
        ClassSectionRules.EnsureActive(classSection);
        ClassSectionRules.EnsureHasRoom(classSection,
            await _classSectionRepository.CountActiveStudentsAsync(classSection.Id, cancellationToken));

        var student = new Student
        {
            AdmNo = request.AdmNo,
            RollNumber = request.RollNumber,
            ClassSectionId = classSection.Id,
            AdmissionDate = request.AdmissionDate,
            Status = "Active",
            Health = new StudentHealth(), // every student has a health row; filled below
        };
        // First academic-history period; saved in the same SaveChanges as the student.
        student.Enrollments.Add(StudentEnrollmentRules.StartNew(classSection, request.RollNumber, request.AdmissionDate));
        ApplyCore(student, request.PhotoUrl, request.FirstName, request.MiddleName, request.LastName, request.Gender,
            request.DateOfBirth, request.Mobile, request.Email, request.AddressLine, request.City, request.State, request.Pincode,
            request.Category, request.Religion, request.PreviousSchool, request.TransportRequired, request.TransportRoute,
            request.Nationality, request.SecondNationality, request.CountryOfBirth, request.PreferredName,
            request.MotherTongue, request.HomeLanguage, request.EnglishProficiency, request.CurriculumTrack, request.AdmissionType,
            request.CustodyArrangement, request.MediaConsent, request.House, request.EalCode, request.FeeConcessionPercent);
        ApplyHealth(student.Health, request.Health);
        foreach (var p in MapPickupPersons(request.PickupPersons)) student.PickupPersons.Add(p);

        await _studentRepository.AddAsync(student, cancellationToken); // one SaveChanges = student + health + pickup persons
        student.ClassSection = classSection;
        return await ToResponseAsync(student, includeSensitive, cancellationToken);
    }

    public async Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, bool includeSensitive, CancellationToken cancellationToken)
    {
        var student = await GetDetailOrThrowAsync(id, cancellationToken);
        var classSection = await GetClassSectionOrThrowAsync(request.ClassSectionId, cancellationToken);

        var activePeriod = student.Enrollments.FirstOrDefault(e => e.Status == EnrollmentStatuses.Active);
        var classChanged = student.ClassSectionId != classSection.Id;
        var rollChanged = student.RollNumber != request.RollNumber;
        var becomesLeft = request.Status == "Left";
        // A student who left (or a legacy row with no open period) coming back needs a fresh period.
        var needsFreshPeriod = !becomesLeft && activePeriod is null;

        // Keeps the history unambiguous: a class move and "left" are two different events.
        if (becomesLeft && classChanged)
            throw new BusinessRuleException(
                "A student cannot be moved to another class and marked as Left in the same edit.");

        // A move to another academic year is a promotion, which has its own year-end process.
        if (classChanged && activePeriod is not null && !becomesLeft &&
            classSection.AcademicYearId != activePeriod.AcademicYearId)
            throw new BusinessRuleException(
                "A student can only be moved to a class in the same academic year here. " +
                "Moving to the next year is done through year-end promotion.");

        // Only a *move to a different class* (or coming back) is checked for status and room,
        // so editing a student whose current class is inactive or at capacity still works.
        if (!becomesLeft && (classChanged || needsFreshPeriod))
        {
            ClassSectionRules.EnsureActive(classSection);
            ClassSectionRules.EnsureHasRoom(classSection,
                await _classSectionRepository.CountActiveStudentsAsync(classSection.Id, cancellationToken));
        }

        if (!becomesLeft && (classChanged || rollChanged))
        {
            if (await _studentRepository.ExistsByRollNumberInClassAsync(classSection.Id, request.RollNumber, cancellationToken))
                throw new ConflictException($"Roll number '{request.RollNumber}' is already in use in this class.");
        }

        var today = StudentEnrollmentRules.Today();
        // Which academic-history change does this edit imply?
        var closeOldAndStartNew = false;
        if (becomesLeft)
        {
            if (activePeriod is not null)
                StudentEnrollmentRules.Close(activePeriod, EnrollmentStatuses.Left, today);
        }
        else if (needsFreshPeriod)
        {
            student.Enrollments.Add(StudentEnrollmentRules.StartNew(classSection, request.RollNumber, today));
        }
        else if (classChanged)
        {
            // Mid-year move: the old period ends today, a new one starts today.
            StudentEnrollmentRules.Close(activePeriod!, EnrollmentStatuses.Transferred, today,
                $"Moved to {classSection.DisplayName}");
            closeOldAndStartNew = true;
        }
        else if (rollChanged)
        {
            // Correcting a roll number inside the same class is not a new period.
            activePeriod!.RollNumber = request.RollNumber;
            activePeriod.UpdatedAt = DateTime.UtcNow;
        }

        // Students keeps a copy of the current placement (written only here and at enrolment).
        // A student who has left keeps their last class and roll number.
        if (!becomesLeft)
        {
            student.RollNumber = request.RollNumber;
            student.ClassSectionId = classSection.Id;
        }
        student.Status = request.Status;
        ApplyCore(student, request.PhotoUrl, request.FirstName, request.MiddleName, request.LastName, request.Gender,
            request.DateOfBirth, request.Mobile, request.Email, request.AddressLine, request.City, request.State, request.Pincode,
            request.Category, request.Religion, request.PreviousSchool, request.TransportRequired, request.TransportRoute,
            request.Nationality, request.SecondNationality, request.CountryOfBirth, request.PreferredName,
            request.MotherTongue, request.HomeLanguage, request.EnglishProficiency, request.CurriculumTrack, request.AdmissionType,
            request.CustodyArrangement, request.MediaConsent, request.House, request.EalCode, request.FeeConcessionPercent);

        // null = "not supplied, leave as is"; a supplied block replaces the stored one.
        if (request.Health is not null)
        {
            student.Health ??= new StudentHealth();
            ApplyHealth(student.Health, request.Health);
            student.Health.UpdatedAt = DateTime.UtcNow;
        }
        if (request.PickupPersons is not null)
        {
            student.PickupPersons.Clear();
            foreach (var p in MapPickupPersons(request.PickupPersons)) student.PickupPersons.Add(p);
        }

        if (closeOldAndStartNew)
        {
            // Two saves in one transaction: the old period must be closed in the database
            // before the new one is inserted, because only one Active period may exist per student.
            await _studentRepository.ExecuteInTransactionAsync(async () =>
            {
                await _studentRepository.SaveChangesAsync(cancellationToken);
                student.Enrollments.Add(StudentEnrollmentRules.StartNew(classSection, request.RollNumber, today));
                await _studentRepository.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
            student.ClassSection = classSection;
            return await ToResponseAsync(student, includeSensitive, cancellationToken);
        }

        await _studentRepository.SaveChangesAsync(cancellationToken);
        student.ClassSection = classSection;
        return await ToResponseAsync(student, includeSensitive, cancellationToken);
    }

    /// <summary>Sets Aadhaar / passport / visa. The controller only lets Students.ViewSensitive callers in.</summary>
    public async Task<StudentResponse> UpdateIdentityAsync(int id, UpdateStudentIdentityRequest request, CancellationToken cancellationToken)
    {
        var student = await GetDetailOrThrowAsync(id, cancellationToken);

        var aadhaar = string.IsNullOrWhiteSpace(request.AadhaarNumber)
            ? null
            : request.AadhaarNumber.Replace(" ", "").Trim();

        if (aadhaar is not null && await _studentRepository.ExistsByAadhaarAsync(aadhaar, student.Id, cancellationToken))
            throw new ConflictException("This Aadhaar number is already recorded for another student.");

        var doc = student.IdentityDocument ??= new StudentIdentityDocument();
        doc.AadhaarNumber = aadhaar;
        doc.PassportNumber = Clean(request.PassportNumber);
        doc.PassportExpiry = request.PassportExpiry;
        doc.VisaType = Clean(request.VisaType);
        doc.VisaExpiry = request.VisaExpiry;
        doc.UpdatedAt = DateTime.UtcNow;

        await _studentRepository.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(student, includeSensitive: true, cancellationToken);
    }

    public async Task<List<StudentEnrollmentResponse>> GetEnrollmentsAsync(int studentId, CancellationToken cancellationToken)
    {
        await GetOrThrowAsync(studentId, cancellationToken); // 404 for an unknown student
        var rows = await _studentRepository.GetEnrollmentsAsync(studentId, cancellationToken);
        return rows.Select(r => new StudentEnrollmentResponse(
            r.Id, r.AcademicYearId, r.AcademicYearName,
            r.ClassSectionId, ClassSection.BuildDisplayName(r.ClassName, r.ClassSection, r.AcademicYearName),
            r.RollNumber, r.StartDate, r.EndDate, r.Status, r.Remarks)).ToList();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var student = await GetOrThrowAsync(id, cancellationToken);
        student.IsDeleted = true;
        student.DeletedAt = DateTime.UtcNow;
        await _studentRepository.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Student> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _studentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Student {id} does not exist.");

    private async Task<Student> GetDetailOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _studentRepository.GetDetailByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Student {id} does not exist.");

    private async Task<ClassSection> GetClassSectionOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var sections = await _classSectionRepository.GetAllAsync(cancellationToken);
        return sections.FirstOrDefault(c => c.Id == id)
            ?? throw new NotFoundException($"Class section {id} does not exist.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Fields shared by create and update. Parent/guardian contact data is NOT here: it lives on Parent.
    private static void ApplyCore(
        Student s, string? photoUrl, string firstName, string? middleName, string lastName, string gender, DateOnly dob,
        string? mobile, string? email, string? addressLine, string? city, string? state, string? pincode,
        string category, string? religion, string? previousSchool, bool transportRequired, string? transportRoute,
        string? nationality, string? secondNationality, string? countryOfBirth, string? preferredName,
        string? motherTongue, string? homeLanguage, string? englishProficiency, string? curriculumTrack, string admissionType,
        string? custodyArrangement, bool mediaConsent, string? house, string? ealCode, decimal? feeConcessionPercent)
    {
        s.PhotoUrl = photoUrl;
        s.FirstName = firstName; s.MiddleName = middleName; s.LastName = lastName;
        s.Gender = gender; s.DateOfBirth = dob;
        s.Mobile = mobile; s.Email = email;
        s.AddressLine = addressLine; s.City = city; s.State = state; s.Pincode = pincode;
        s.Category = category; s.Religion = religion; s.PreviousSchool = previousSchool;
        s.TransportRequired = transportRequired; s.TransportRoute = transportRoute;
        s.Nationality = nationality; s.SecondNationality = secondNationality;
        s.CountryOfBirth = countryOfBirth; s.PreferredName = preferredName;
        s.MotherTongue = motherTongue; s.HomeLanguage = homeLanguage;
        s.EnglishProficiency = englishProficiency; s.CurriculumTrack = curriculumTrack;
        s.AdmissionType = admissionType;
        s.CustodyArrangement = custodyArrangement; s.MediaConsent = mediaConsent;
        s.House = house; s.EalCode = ealCode;
        s.FeeConcessionPercent = feeConcessionPercent;
    }

    private static void ApplyHealth(StudentHealth h, StudentHealthDto? dto)
    {
        if (dto is null) return;
        h.BloodGroup = dto.BloodGroup;
        h.Allergies = dto.Allergies;
        h.DietaryRequirements = dto.DietaryRequirements;
        h.MedicalNotes = dto.MedicalNotes;
        h.SpecialEducationalNeeds = dto.SpecialEducationalNeeds;
        h.InsuranceProvider = dto.InsuranceProvider;
        h.InsurancePolicyExpiry = dto.InsurancePolicyExpiry;
    }

    private static List<StudentPickupPerson> MapPickupPersons(List<PickupPersonRequest>? requests) =>
        (requests ?? new List<PickupPersonRequest>())
            .Select(p => new StudentPickupPerson
            {
                Name = p.Name.Trim(),
                Relation = p.Relation.Trim(),
                Phone = p.Phone.Trim(),
                IdNote = Clean(p.IdNote),
            })
            .ToList();

    private async Task<StudentResponse> ToResponseAsync(Student s, bool includeSensitive, CancellationToken cancellationToken)
    {
        var links = s.Id == 0
            ? new List<StudentGuardian>()
            : await _guardianRepository.GetForStudentAsync(s.Id, cancellationToken);

        var guardians = links
            .OrderByDescending(l => l.IsPrimaryContact).ThenBy(l => l.Parent.Name)
            .Select(l => new StudentGuardianResponse(
                l.ParentId, l.Parent.Name, l.Parent.Mobile, l.Parent.Email, l.RelationType, l.IsPrimaryContact))
            .ToList();

        var h = s.Health;
        var health = new StudentHealthDto(
            h?.BloodGroup, h?.Allergies, h?.DietaryRequirements, h?.MedicalNotes,
            h?.SpecialEducationalNeeds, h?.InsuranceProvider, h?.InsurancePolicyExpiry);

        var d = s.IdentityDocument;
        var identity = new StudentIdentityResponse(
            includeSensitive ? d?.AadhaarNumber : SensitiveDataMasker.MaskTail(d?.AadhaarNumber),
            includeSensitive ? d?.PassportNumber : SensitiveDataMasker.MaskTail(d?.PassportNumber),
            d?.PassportExpiry, d?.VisaType, d?.VisaExpiry,
            IsMasked: !includeSensitive);

        return new StudentResponse(
            s.Id, s.AdmNo, s.RollNumber, s.ClassSectionId, s.ClassSection.DisplayName,
            s.AdmissionDate, s.Status, s.PhotoUrl,
            s.FirstName, s.MiddleName, s.LastName, s.Gender, s.DateOfBirth,
            s.Mobile, s.Email, s.AddressLine, s.City, s.State, s.Pincode,
            s.Category, s.Religion, s.PreviousSchool,
            s.TransportRequired, s.TransportRoute,
            s.Nationality, s.SecondNationality, s.CountryOfBirth, s.PreferredName,
            s.MotherTongue, s.HomeLanguage, s.EnglishProficiency, s.CurriculumTrack, s.AdmissionType,
            s.CustodyArrangement, s.MediaConsent,
            s.House, s.EalCode, s.FeeConcessionPercent,
            s.Admission?.RegNo, s.AdmissionId,
            health, identity,
            s.PickupPersons.OrderBy(p => p.Id)
                .Select(p => new PickupPersonResponse(p.Id, p.Name, p.Relation, p.Phone, p.IdNote)).ToList(),
            guardians);
    }
}