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
    private readonly IConcurrencyGuard _concurrency;
    private readonly IAddressService _addressService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUser _currentUser;

    public AdmissionService(
        IAdmissionRepository admissionRepository,
        IClassSectionRepository classSectionRepository,
        IStudentRepository studentRepository,
        IParentRepository parentRepository,
        IStudentGuardianRepository studentGuardianRepository,
        IConcurrencyGuard concurrency,
        IAddressService addressService,
        IDocumentRepository documentRepository,
        ICurrentUser currentUser)
    {
        _admissionRepository = admissionRepository;
        _classSectionRepository = classSectionRepository;
        _studentRepository = studentRepository;
        _parentRepository = parentRepository;
        _studentGuardianRepository = studentGuardianRepository;
        _concurrency = concurrency;
        _addressService = addressService;
        _documentRepository = documentRepository;
        _currentUser = currentUser;
    }

    public async Task<List<AdmissionResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var admissions = await _admissionRepository.GetAllAsync(cancellationToken);
        return admissions.Select(ToResponse).ToList();
    }

    public async Task<List<DeletedAdmissionResponse>> GetDeletedAsync(CancellationToken cancellationToken)
    {
        var rows = await _admissionRepository.GetDeletedAsync(cancellationToken);
        return rows.Select(r => new DeletedAdmissionResponse(
            r.Id, r.RegNo, $"{r.FirstName} {r.LastName}", $"{r.ClassName} ({r.AcademicYearName})",
            r.Status, r.Phone, r.DeletedAt, r.DeletedByUsername)).ToList();
    }

    public async Task<AdmissionResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        return ToResponse(admission);
    }

    public async Task<AdmissionResponse> CreateAsync(CreateAdmissionRequest request, CancellationToken cancellationToken, bool canLinkParents = false)
    {
        var classSection = await GetClassSectionOrThrowAsync(request.AppliedForClassSectionId, cancellationToken);
        ClassSectionRules.EnsureActive(classSection);
        var addresses = await _addressService.BuildAsync(request.Addresses ?? new(), cancellationToken);
        var guardians = await MapGuardiansAsync(request.Guardians, new HashSet<int>(), canLinkParents, cancellationToken);

        // One transaction: the application, its guardians, addresses and the RegNo update succeed or fail together.
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
                Remarks = request.Remarks,
                Addresses = addresses,
                RegNo = "PENDING", // placeholder until Id is assigned — see below
                Guardians = guardians,
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

    public async Task<AdmissionResponse> UpdateAsync(int id, UpdateAdmissionRequest request, CancellationToken cancellationToken, bool canLinkParents = false)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        RequireEditable(admission);
        _concurrency.Expect(admission, request.RowVersion); // 409 if someone saved this application after it was loaded
        var classSection = await GetClassSectionOrThrowAsync(request.AppliedForClassSectionId, cancellationToken);

        // Only a *changed* class is checked, so editing an old applicant whose
        // class has since been deactivated still works.
        if (admission.AppliedForClassSectionId != classSection.Id)
            ClassSectionRules.EnsureActive(classSection);

        // null = addresses not sent: keep the stored ones.
        var addresses = request.Addresses is null
            ? null
            : await _addressService.BuildAsync(request.Addresses, cancellationToken);

        // G2: parents already linked to this application may stay linked without Parents.View; a NEW link needs it.
        // Read before the guardians are cleared below.
        var alreadyLinked = admission.Guardians.Where(g => g.ParentId is not null).Select(g => g.ParentId!.Value).ToHashSet();
        var newGuardians = await MapGuardiansAsync(request.Guardians, alreadyLinked, canLinkParents, cancellationToken);

        return await _admissionRepository.ExecuteInTransactionAsync(async () =>
        {
            // Guardians are replaced as a set. (Only Registered/Admitted applications get here; once
            // enrolled, the guardians have become Parents of the student and are edited there.)
            if (admission.Guardians.Count > 0)
            {
                // Separate save first so the "one primary contact" unique index never sees old + new rows together.
                admission.Guardians.Clear();
                await _admissionRepository.SaveChangesAsync(cancellationToken);
            }
            foreach (var guardian in newGuardians)
                admission.Guardians.Add(guardian);

            if (addresses is not null)
                await _addressService.ReplaceAsync(admission.Addresses, addresses, cancellationToken);

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
    /// G2: the registration form's "Find an existing parent". Needs at least 2 letters or 3 digits, so one keystroke
    /// does not list the whole school. Returns the linked children's names (the caller holds Parents.View).
    /// </summary>
    public async Task<List<ParentCandidateResponse>> SearchParentsAsync(string? query, int take, CancellationToken cancellationToken)
    {
        var text = (query ?? string.Empty).Trim();
        var digits = new string(text.Where(char.IsDigit).ToArray());
        var nameText = text.Length >= 2 ? text : null;
        var digitText = digits.Length >= 3 ? digits : null;
        take = Math.Clamp(take, 1, 10);

        var rows = await _admissionRepository.SearchParentsAsync(nameText, digitText, take, cancellationToken);
        if (rows.Count == 0)
            return new List<ParentCandidateResponse>();

        var children = await _admissionRepository.GetLinkedChildrenAsync(rows.Select(r => r.ParentId).ToList(), cancellationToken);
        var byParent = children.GroupBy(c => c.ParentId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.StudentName).Distinct().ToList());

        return rows.Select(r => new ParentCandidateResponse(
            r.ParentId, r.Name, r.Mobile, r.Email, byParent.GetValueOrDefault(r.ParentId) ?? new List<string>())).ToList();
    }

    /// <summary>
    /// For each guardian on the application, find existing Parents with the same mobile number
    /// and say what we suggest. Nothing is linked here — staff confirm every decision at enrolment.
    /// G2: a guardian registered as an existing parent is suggested that parent first (even if the number was
    /// changed since); if that parent has been deleted the suggestion is ParentRemoved.
    /// </summary>
    public async Task<GuardianMatchesResponse> GetGuardianMatchesAsync(int id, CancellationToken cancellationToken)
    {
        var admission = await GetOrThrowAsync(id, cancellationToken);
        RequireStatus(admission, "Admitted", "match guardians for");

        var rows = await _admissionRepository.GetParentCandidatesAsync(id, cancellationToken);

        var linkedParents = new Dictionary<int, Parent>();
        foreach (var linkedId in admission.Guardians.Where(g => g.ParentId is not null).Select(g => g.ParentId!.Value).Distinct())
        {
            var linkedParent = await _parentRepository.GetByIdAsync(linkedId, cancellationToken); // null when deleted
            if (linkedParent is not null)
                linkedParents[linkedId] = linkedParent;
        }

        var parentIds = rows.Select(r => r.ParentId).Concat(linkedParents.Keys).Distinct().ToList();
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

            Parent? linked = g.ParentId is int lid && linkedParents.TryGetValue(lid, out var lp) ? lp : null;
            if (linked is not null)
            {
                candidates.RemoveAll(c => c.ParentId == linked.Id);
                candidates.Insert(0, new ParentCandidateResponse(
                    linked.Id, linked.Name, linked.Mobile, linked.Email,
                    childrenByParent.GetValueOrDefault(linked.Id) ?? new List<string>()));
            }

            var suggestion =
                g.ParentId is not null && linked is null ? GuardianActions.ParentRemoved :
                linked is not null ? GuardianActions.UseExisting :
                string.IsNullOrWhiteSpace(g.Mobile) ? GuardianActions.MissingMobile :
                candidates.Count == 0 ? GuardianActions.CreateNew :
                candidates.Count == 1 ? GuardianActions.UseExisting :
                GuardianActions.MustChoose;

            return new GuardianMatchResponse(g.Id, g.RelationType, g.Name, g.Mobile, suggestion, candidates, g.ParentId);
        }).ToList();

        // Father and mother sharing one number would both be pointed at the same Parent;
        // one Parent cannot be two guardians of a student, so make staff choose.
        // (A guardian registered as that very parent keeps it; only the OTHER guardians pointed at it must choose.)
        var contested = result
            .Where(m => m.SuggestedAction == GuardianActions.UseExisting)
            .GroupBy(m => m.Candidates[0].ParentId)
            .Where(grp => grp.Count() > 1)
            .SelectMany(grp => grp)
            .Where(m => m.LinkedParentId != m.Candidates[0].ParentId)
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
        // Student, StudentHealth, new Parents, StudentGuardian links, the documents' move and the status change.
        return await _admissionRepository.ExecuteInTransactionAsync(async () =>
        {
            // U2: one enrolment per application at a time. The second request waits here, then reads
            // the application fresh, finds it "Enrolled" and gets a clear 409 below. (Without the lock
            // the rowversion check and the unique index on Students.AdmissionId still stop a second
            // student, but with a vaguer message.)
            await _admissionRepository.LockForEnrolmentAsync(id, cancellationToken);
            var admission = await GetOrThrowAsync(id, cancellationToken);
            RequireStatus(admission, "Admitted", "enroll");

            var allottedClassSection = await GetClassSectionOrThrowAsync(request.AllottedClassSectionId, cancellationToken);

            // Business rules: the section must be active and not full.
            // The lock makes "count the seats, then take one" safe when two people enrol at once.
            await _classSectionRepository.LockSeatsAsync(allottedClassSection.Id, cancellationToken);
            ClassSectionRules.EnsureActive(allottedClassSection);
            ClassSectionRules.EnsureHasRoom(allottedClassSection,
                await _classSectionRepository.CountActiveStudentsAsync(allottedClassSection.Id, cancellationToken));

            // Numbers left blank are generated here, inside the transaction and behind a lock, so two
            // enrolments at the same time cannot get the same one. The section lock above already
            // covers roll numbers; admission numbers have their own lock per year.
            var admissionNumber = request.AdmissionNumber?.Trim();
            if (string.IsNullOrEmpty(admissionNumber))
            {
                var year = request.AdmissionDate.Year;
                await _admissionRepository.LockAdmissionNumbersAsync(year, cancellationToken);
                var prefix = NumberFormats.AdmissionPrefix(year);
                admissionNumber = NumberFormats.AdmissionNumber(
                    year, await _studentRepository.GetMaxAdmNoSerialAsync(prefix, cancellationToken) + 1);
            }
            var rollNumber = request.RollNumber?.Trim();
            if (string.IsNullOrEmpty(rollNumber))
            {
                var prefix = NumberFormats.RollPrefix(allottedClassSection);
                rollNumber = NumberFormats.RollNumber(
                    prefix, await _studentRepository.GetMaxRollSerialAsync(allottedClassSection.Id, prefix, cancellationToken) + 1);
            }

            // Friendly uniqueness errors up front (for typed numbers); the unique indexes (caught in the repository) are the safety net.
            if (await _studentRepository.ExistsByAdmNoAsync(admissionNumber, cancellationToken))
                throw new ConflictException($"Admission number '{admissionNumber}' is already used by another student.");
            if (await _studentRepository.ExistsByRollNumberInClassAsync(allottedClassSection.Id, rollNumber, cancellationToken))
                throw new ConflictException($"Roll number '{rollNumber}' is already taken in {allottedClassSection.DisplayName}.");

            // Validates every guardian decision against the real application. No writes yet.
            var plan = await BuildGuardianPlanAsync(admission, request.Guardians, cancellationToken);

            admission.RollNumber = rollNumber;
            admission.AdmissionNumber = admissionNumber;
            admission.AdmissionDate = request.AdmissionDate;
            admission.EntryPoint = request.EntryPoint;
            admission.TransportRequired = request.TransportRequired;
            admission.AllottedClassSectionId = allottedClassSection.Id;
            admission.Status = "Enrolled";

            // 1. Student (+ its health row). Portal login credentials remain a separate, deliberate step.
            var student = new Student
            {
                AdmissionId = admission.Id,
                AdmNo = admissionNumber,
                RollNumber = rollNumber,
                ClassSectionId = allottedClassSection.Id,
                AdmissionDate = request.AdmissionDate,
                Status = "Active",
                FirstName = admission.FirstName,
                MiddleName = admission.MiddleName,
                LastName = admission.LastName,
                Gender = admission.Gender,
                DateOfBirth = admission.DateOfBirth,
                // G1a: the student gets copies; the application keeps the addresses it was submitted with.
                Addresses = AddressMapping.CopyOf(admission.Addresses),
                Religion = admission.Religion,
                Category = string.IsNullOrWhiteSpace(admission.Category) ? "General" : admission.Category,
                AdmissionType = admission.AdmissionType,
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
                    StudentEnrollmentRules.StartNew(allottedClassSection, rollNumber, request.AdmissionDate),
                },
                Health = new StudentHealth
                {
                    BloodGroup = admission.BloodGroup,
                    MedicalNotes = admission.MedicalNotes,
                    Allergies = request.Allergies,
                },
            };
            await _studentRepository.AddAsync(student, cancellationToken);

            // G1b: the application's documents MOVE to the student (unlike addresses, which are copied):
            // there is one stored file and, once enrolled, it belongs to the student. Saved with step 4.
            foreach (var document in await _documentRepository.GetForAdmissionAsync(admission.Id, cancellationToken))
            {
                document.AdmissionId = null;
                document.StudentId = student.Id;
            }

            // 2 + 3. Create or reuse each Parent, then link it to the student.
            // G2: remember guardians whose registered parent differs from the parent they became (see below).
            var changedLinks = new List<(AdmissionGuardian Guardian, int ParentId)>();
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

                if (item.Guardian.ParentId is not null && item.Guardian.ParentId != parent.Id)
                    changedLinks.Add((item.Guardian, parent.Id));
            }

            // G2: staff chose someone other than the parent picked at registration. Writing the final parent onto the
            // guardian row makes the audit trail record it automatically (AdmissionGuardians Update: ParentId old -> new,
            // with the user and time). Two guardians swapping parents would briefly break the "same parent twice"
            // unique index, so in that one case the old links are cleared (and saved) first.
            if (changedLinks.Count > 0)
            {
                var leaving = changedLinks.Select(c => c.Guardian.ParentId!.Value).ToHashSet();
                if (changedLinks.Any(c => leaving.Contains(c.ParentId)))
                {
                    foreach (var (guardian, _) in changedLinks)
                        guardian.ParentId = null;
                    await _admissionRepository.SaveChangesAsync(cancellationToken);
                }
                foreach (var (guardian, parentId) in changedLinks)
                    guardian.ParentId = parentId;
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
        // An enrolled application is the record of how the student joined: it is kept for good.
        // (Registered, Admitted and Rejected applications can still be deleted.)
        if (admission.Status == "Enrolled")
            throw new ConflictException(
                $"Application '{admission.RegNo}' is enrolled and cannot be deleted: it is the admission record of the student.");

        admission.IsDeleted = true;
        admission.DeletedAt = DateTime.UtcNow;
        admission.DeletedBy = _currentUser.UserId;
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

        // G2: a guardian registered as an existing parent whose record has since been deleted. Whatever decision came
        // with the request, do NOT quietly create a duplicate: staff fix the application first.
        var removed = new List<string>();
        foreach (var guardian in guardians.Where(g => g.ParentId is not null))
            if (await _parentRepository.GetByIdAsync(guardian.ParentId!.Value, cancellationToken) is null)
                removed.Add(guardian.Name);
        if (removed.Count > 0)
            throw new BusinessRuleException(
                $"The parent record linked to {string.Join(", ", removed)} has been deleted. Open the application, choose "
                + "'Use a different person' for that guardian (or pick another parent), save, and enrol again.");

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

    /// <summary>
    /// Builds the guardian rows. G2: a request naming a ParentId becomes that parent: name, mobile and email are
    /// COPIED from the parent record and whatever the form sent for them is ignored.
    /// </summary>
    /// <param name="alreadyLinked">Parents this application is already linked to (they need no extra permission).</param>
    private async Task<List<AdmissionGuardian>> MapGuardiansAsync(
        List<AdmissionGuardianRequest>? requests, ISet<int> alreadyLinked, bool canLinkParents, CancellationToken cancellationToken)
    {
        var list = requests ?? new List<AdmissionGuardianRequest>();

        var linkedIds = list.Where(g => g.ParentId is not null).Select(g => g.ParentId!.Value).ToList();
        if (linkedIds.Count != linkedIds.Distinct().Count())
            throw new BusinessRuleException("The same parent cannot be added twice to one application.");

        // Permission first, so someone without it learns nothing about which parent ids exist.
        if (!canLinkParents && linkedIds.Any(i => !alreadyLinked.Contains(i)))
            throw new ForbiddenException("Choosing an existing parent needs permission to view parents.");

        var parents = new Dictionary<int, Parent>();
        foreach (var parentId in linkedIds)
            parents[parentId] = await _parentRepository.GetByIdAsync(parentId, cancellationToken)
                ?? throw new NotFoundException($"Parent {parentId} does not exist (it may have been deleted).");

        return list.Select(g =>
        {
            var parent = g.ParentId is int pid ? parents[pid] : null;
            return new AdmissionGuardian
            {
                RelationType = g.RelationType,
                Name = parent?.Name ?? g.Name.Trim(),
                Mobile = parent is not null ? parent.Mobile : string.IsNullOrWhiteSpace(g.Mobile) ? null : g.Mobile.Trim(),
                Email = parent is not null ? parent.Email : string.IsNullOrWhiteSpace(g.Email) ? null : g.Email.Trim(),
                IsPrimaryContact = g.IsPrimaryContact,
                ParentId = parent?.Id,
            };
        }).ToList();
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

    /// <summary>
    /// U2: an application can be edited only while it is in progress (Registered or Admitted).
    /// Enrolled: the data now lives on the student record. Rejected: it is closed.
    /// </summary>
    public static bool IsEditable(string status) => status is "Registered" or "Admitted";

    private static void RequireEditable(Admission admission)
    {
        if (!IsEditable(admission.Status))
            throw new ConflictException(admission.Status == "Enrolled"
                ? $"Application '{admission.RegNo}' is enrolled and can no longer be edited. Edit the student record instead."
                : $"Application '{admission.RegNo}' is {admission.Status.ToLowerInvariant()} and can no longer be edited.");
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
        // Registration only asks for the class; the section is chosen at enrolment (AllottedClassSectionName).
        a.AppliedForClassSectionId, $"{a.AppliedForClassSection.Name} ({a.AcademicYear.Name})",
        a.AdmissionType, a.PreviousSchool, a.Phone, a.Email, a.RegistrationDate,
        a.Status, a.RejectionReason,
        AddressMapping.ToResponses(a.Addresses),
        a.AdmissionFee, a.AdmissionFeeReference, a.BloodGroup, a.Religion, a.Category,
        a.MedicalNotes, a.Remarks,
        a.RollNumber, a.AdmissionNumber, a.AdmissionDate, a.EntryPoint, a.TransportRequired,
        a.AllottedClassSectionId, a.AllottedClassSection?.DisplayName,
        a.Student?.Id,
        a.Guardians.OrderBy(g => g.Id)
            .Select(g => new AdmissionGuardianResponse(g.Id, g.RelationType, g.Name, g.Mobile, g.Email, g.IsPrimaryContact, g.ParentId))
            .ToList(),
        Convert.ToBase64String(a.RowVersion));
}
