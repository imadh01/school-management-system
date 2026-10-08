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
    private readonly IParentRepository _parentRepository;
    private readonly IStudentGuardianRepository _studentGuardianRepository;

    public AdmissionService(
        IAdmissionRepository admissionRepository,
        IClassSectionRepository classSectionRepository,
        IStudentRepository studentRepository,
        IParentRepository parentRepository,
        IStudentGuardianRepository studentGuardianRepository)
    {
        _admissionRepository = admissionRepository;
        _classSectionRepository = classSectionRepository;
        _studentRepository = studentRepository;
        _parentRepository = parentRepository;
        _studentGuardianRepository = studentGuardianRepository;
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

        // One transaction: the application, its guardians and the RegNo update succeed or fail together.
        return await _admissionRepository.ExecuteInTransactionAsync(async () =>
        {
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
                AddressLine = request.AddressLine,
                City = request.City,
                State = request.State,
                Pincode = request.Pincode,
                Remarks = request.Remarks,
                RegNo = "PENDING", // placeholder until Id is assigned — see below
                Guardians = MapGuardians(request.Guardians),
            };

            await _admissionRepository.AddAsync(admission, cancellationToken); // Id now assigned by identity column

            // Matches the prototype exactly: RegNo = 'REG-' + year + '-' + zero-padded id,
            // not a per-year-reset counter — it's literally the row's own sequential id.
            admission.RegNo = $"REG-{DateTime.UtcNow.Year}-{admission.Id:D4}";
            await _admissionRepository.SaveChangesAsync(cancellationToken);

            admission.AcademicYear = classSection.AcademicYear;
            admission.AppliedForClassSection = classSection;
            return ToResponse(admission);
        }, cancellationToken);
    }

    public async Task<AdmissionResponse> UpdateAsync(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        var classSection = await GetClassSectionOrThrowAsync(request.AppliedForClassSectionId, cancellationToken);

        // Only a *changed* class is checked, so editing an old applicant whose
        // class has since been deactivated still works.
        if (admission.AppliedForClassSectionId != classSection.Id)
            ClassSectionRules.EnsureActive(classSection);

        return await _admissionRepository.ExecuteInTransactionAsync(async () =>
        {
            // Guardians are replaced as a set. Once enrolled, the guardians have already become
            // Parents of the student, so editing the application's list would change nothing real
            // — it is left as it was.
            if (admission.Status != "Enrolled")
            {
                if (admission.Guardians.Count > 0)
                {
                    // Separate save first so the "one primary contact" unique index never sees old + new rows together.
                    admission.Guardians.Clear();
                    await _admissionRepository.SaveChangesAsync(cancellationToken);
                }
                foreach (var guardian in MapGuardians(request.Guardians))
                    admission.Guardians.Add(guardian);
            }

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
            admission.AddressLine = request.AddressLine;
            admission.City = request.City;
            admission.State = request.State;
            admission.Pincode = request.Pincode;
            admission.Remarks = request.Remarks;

            await _admissionRepository.SaveChangesAsync(cancellationToken);
            return ToResponse(admission);
        }, cancellationToken);
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

    /// <summary>
    /// For each guardian on the application, find existing Parents with the same mobile number
    /// and say what we suggest. Nothing is linked here — staff confirm every decision at enrolment.
    /// </summary>
    public async Task<GuardianMatchesResponse> GetGuardianMatchesAsync(int id, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        RequireStatus(admission, "Admitted", "match guardians for");

        var rows = await _admissionRepository.GetParentCandidatesAsync(id, cancellationToken);
        var parentIds = rows.Select(r => r.ParentId).Distinct().ToList();
        var children = parentIds.Count == 0
            ? new List<ParentChildRow>()
            : await _admissionRepository.GetLinkedChildrenAsync(parentIds, cancellationToken);

        var childrenByParent = children
            .GroupBy(c => c.ParentId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.StudentName).Distinct().ToList());

        var result = admission.Guardians.OrderBy(g => g.Id).Select(g =>
        {
            var candidates = rows
                .Where(r => r.AdmissionGuardianId == g.Id)
                .Select(r => new ParentCandidateResponse(
                    r.ParentId, r.Name, r.Mobile, r.Email,
                    childrenByParent.GetValueOrDefault(r.ParentId) ?? new List<string>()))
                .ToList();

            var suggestion =
                string.IsNullOrWhiteSpace(g.Mobile) ? GuardianActions.MissingMobile :
                candidates.Count == 0 ? GuardianActions.CreateNew :
                candidates.Count == 1 ? GuardianActions.UseExisting :
                GuardianActions.MustChoose;

            return new GuardianMatchResponse(g.Id, g.RelationType, g.Name, g.Mobile, suggestion, candidates);
        }).ToList();

        // Father and mother sharing one number would both be pointed at the same Parent;
        // one Parent cannot be two guardians of a student, so make staff choose.
        var contested = result
            .Where(m => m.SuggestedAction == GuardianActions.UseExisting)
            .GroupBy(m => m.Candidates[0].ParentId)
            .Where(grp => grp.Count() > 1)
            .SelectMany(grp => grp)
            .Select(m => m.AdmissionGuardianId)
            .ToHashSet();

        result = result
            .Select(m => contested.Contains(m.AdmissionGuardianId) ? m with { SuggestedAction = GuardianActions.MustChoose } : m)
            .ToList();

        return new GuardianMatchesResponse(admission.Id, result);
    }

    public async Task<AdmissionResponse> EnrollAsync(int id, EnrollAdmissionRequest request, CancellationToken cancellationToken)
    {
        // Everything below runs in ONE transaction. Any exception rolls all of it back:
        // Student, StudentHealth, new Parents, StudentGuardian links and the status change.
        return await _admissionRepository.ExecuteInTransactionAsync(async () =>
        {
            var admission = await GetOrThrowAsync(id, cancellationToken);
            RequireStatus(admission, "Admitted", "enroll");

            var allottedClassSection = await GetClassSectionOrThrowAsync(request.AllottedClassSectionId, cancellationToken);

            // Business rules: the section must be active and not full.
            ClassSectionRules.EnsureActive(allottedClassSection);
            ClassSectionRules.EnsureHasRoom(allottedClassSection,
                await _classSectionRepository.CountActiveStudentsAsync(allottedClassSection.Id, cancellationToken));

            // Friendly uniqueness errors up front; the unique indexes (caught in the repository) are the safety net.
            if (await _studentRepository.ExistsByAdmNoAsync(request.AdmissionNumber, cancellationToken))
                throw new ConflictException($"Admission number '{request.AdmissionNumber}' is already used by another student.");
            if (await _studentRepository.ExistsByRollNumberInClassAsync(allottedClassSection.Id, request.RollNumber, cancellationToken))
                throw new ConflictException($"Roll number '{request.RollNumber}' is already taken in {allottedClassSection.DisplayName}.");

            // Validates every guardian decision against the real application. No writes yet.
            var plan = await BuildGuardianPlanAsync(admission, request.Guardians, cancellationToken);

            admission.RollNumber = request.RollNumber;
            admission.AdmissionNumber = request.AdmissionNumber;
            admission.AdmissionDate = request.AdmissionDate;
            admission.EntryPoint = request.EntryPoint;
            admission.TransportRequired = request.TransportRequired;
            admission.AllottedClassSectionId = allottedClassSection.Id;
            admission.Status = "Enrolled";

            // 1. Student (+ its health row). Portal login credentials remain a separate, deliberate step.
            var student = new Student
            {
                AdmissionId = admission.Id,
                AdmNo = request.AdmissionNumber,
                RollNumber = request.RollNumber,
                ClassSectionId = allottedClassSection.Id,
                AdmissionDate = request.AdmissionDate,
                Status = "Active",
                FirstName = admission.FirstName,
                MiddleName = admission.MiddleName,
                LastName = admission.LastName,
                Gender = admission.Gender,
                DateOfBirth = admission.DateOfBirth,
                AddressLine = admission.AddressLine,
                City = admission.City,
                State = admission.State,
                Pincode = admission.Pincode,
                Religion = admission.Religion,
                Category = string.IsNullOrWhiteSpace(admission.Category) ? "General" : admission.Category,
                PreviousSchool = admission.PreviousSchool,
                TransportRequired = request.TransportRequired,
                Nationality = request.Nationality,
                CurriculumTrack = request.CurriculumTrack,
                EnglishProficiency = request.EnglishProficiency,
                EalCode = request.EalCode,
                House = request.House,
                // First academic-history period, saved together with the student.
                Enrollments =
                {
                    StudentEnrollmentRules.StartNew(allottedClassSection, request.RollNumber, request.AdmissionDate),
                },
                Health = new StudentHealth
                {
                    BloodGroup = admission.BloodGroup,
                    MedicalNotes = admission.MedicalNotes,
                    Allergies = request.Allergies,
                },
            };
            await _studentRepository.AddAsync(student, cancellationToken);

            // 2 + 3. Create or reuse each Parent, then link it to the student.
            foreach (var item in plan)
            {
                var parent = item.ExistingParent;
                if (parent is null)
                {
                    parent = new Parent
                    {
                        Name = item.Guardian.Name,
                        Mobile = item.Guardian.Mobile!, // plan guarantees a mobile
                        Email = item.Guardian.Email,
                    };
                    await _parentRepository.AddAsync(parent, cancellationToken);
                }

                await _studentGuardianRepository.AddLinkAsync(new StudentGuardian
                {
                    StudentId = student.Id,
                    ParentId = parent.Id,
                    RelationType = item.Guardian.RelationType,
                    IsPrimaryContact = item.IsPrimary,
                }, cancellationToken);
            }

            // 4. Admission is now Enrolled.
            await _admissionRepository.SaveChangesAsync(cancellationToken);

            admission.Student = student;
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

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        admission.IsDeleted = true;
        admission.DeletedAt = DateTime.UtcNow;
        await _admissionRepository.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- helpers

    private sealed record GuardianPlanItem(AdmissionGuardian Guardian, Parent? ExistingParent, bool IsPrimary);

    /// <summary>
    /// Server-side authority over the enrolment's guardian decisions. The React confirmation
    /// step is only a convenience: every rule is re-checked here.
    /// </summary>
    private async Task<List<GuardianPlanItem>> BuildGuardianPlanAsync(
        Admission admission, List<GuardianDecision> decisions, CancellationToken cancellationToken)
    {
        var guardians = admission.Guardians.OrderBy(g => g.Id).ToList();

        // Business decision B: a guardian is only required at enrolment, not at registration.
        if (guardians.Count == 0)
            throw new BusinessRuleException("At least one guardian is required to enrol. Add a guardian to the application first.");

        // Business decision A: matching depends on the mobile number, and a Parent must have one.
        var withoutMobile = guardians.Where(g => string.IsNullOrWhiteSpace(g.Mobile)).Select(g => g.Name).ToList();
        if (withoutMobile.Count > 0)
            throw new BusinessRuleException(
                $"A mobile number is required before enrolling. Missing for: {string.Join(", ", withoutMobile)}.");

        if (decisions.Select(d => d.AdmissionGuardianId).Distinct().Count() != decisions.Count)
            throw new BusinessRuleException("Each guardian can have only one decision.");

        var guardianIds = guardians.Select(g => g.Id).ToHashSet();
        if (decisions.Any(d => !guardianIds.Contains(d.AdmissionGuardianId)))
            throw new BusinessRuleException("A decision refers to a guardian that is not on this application.");

        var undecided = guardians.Where(g => decisions.All(d => d.AdmissionGuardianId != g.Id)).Select(g => g.Name).ToList();
        if (undecided.Count > 0)
            throw new BusinessRuleException($"A decision is required for every guardian. Missing for: {string.Join(", ", undecided)}.");

        // Exactly one primary contact (the database enforces "at most one").
        var primaryId = guardians.FirstOrDefault(g => g.IsPrimaryContact)?.Id ?? guardians[0].Id;

        var usedParentIds = new HashSet<int>();
        var plan = new List<GuardianPlanItem>();

        foreach (var guardian in guardians)
        {
            var decision = decisions.First(d => d.AdmissionGuardianId == guardian.Id);
            Parent? existing = null;

            switch (decision.Action)
            {
                case GuardianActions.UseExisting:
                    if (decision.ParentId is null)
                        throw new BusinessRuleException($"Choose which existing parent to use for {guardian.Name}.");
                    if (!usedParentIds.Add(decision.ParentId.Value))
                        throw new BusinessRuleException("The same parent cannot be linked as two different guardians of one student.");
                    existing = await _parentRepository.GetByIdAsync(decision.ParentId.Value, cancellationToken)
                        ?? throw new NotFoundException($"Parent {decision.ParentId} does not exist.");
                    break;

                case GuardianActions.CreateNew:
                    if (decision.ParentId is not null)
                        throw new BusinessRuleException($"A new parent cannot also name an existing parent ({guardian.Name}).");
                    break;

                default:
                    throw new BusinessRuleException($"Unknown guardian action '{decision.Action}'.");
            }

            plan.Add(new GuardianPlanItem(guardian, existing, guardian.Id == primaryId));
        }

        return plan;
    }

    private static List<AdmissionGuardian> MapGuardians(List<AdmissionGuardianRequest>? requests) =>
        (requests ?? new List<AdmissionGuardianRequest>())
            .Select(g => new AdmissionGuardian
            {
                RelationType = g.RelationType,
                Name = g.Name.Trim(),
                Mobile = string.IsNullOrWhiteSpace(g.Mobile) ? null : g.Mobile.Trim(),
                Email = string.IsNullOrWhiteSpace(g.Email) ? null : g.Email.Trim(),
                IsPrimaryContact = g.IsPrimaryContact,
            })
            .ToList();

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

    private static AdmissionResponse ToResponse(Admission a) => new(
        a.Id, a.RegNo, a.FirstName, a.MiddleName, a.LastName, a.Gender, a.DateOfBirth,
        a.AcademicYear.Name,
        a.AppliedForClassSectionId, a.AppliedForClassSection.DisplayName,
        a.AdmissionType, a.PreviousSchool, a.Phone, a.Email, a.RegistrationDate,
        a.Status, a.RejectionReason,
        a.AddressLine, a.City, a.State, a.Pincode,
        a.AdmissionFee, a.AdmissionFeeReference, a.BloodGroup, a.Religion, a.Category,
        a.MedicalNotes, a.Remarks,
        a.RollNumber, a.AdmissionNumber, a.AdmissionDate, a.EntryPoint, a.TransportRequired,
        a.AllottedClassSectionId, a.AllottedClassSection?.DisplayName,
        a.Student?.Id,
        a.Guardians.OrderBy(g => g.Id)
            .Select(g => new AdmissionGuardianResponse(g.Id, g.RelationType, g.Name, g.Mobile, g.Email, g.IsPrimaryContact))
            .ToList());
}