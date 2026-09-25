using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IAdmissionRepository _admissionRepository;
    private readonly IClassSectionRepository _classSectionRepository;

    public StudentService(
        IStudentRepository studentRepository,
        IAdmissionRepository admissionRepository,
        IClassSectionRepository classSectionRepository)
    {
        _studentRepository = studentRepository;
        _admissionRepository = admissionRepository;
        _classSectionRepository = classSectionRepository;
    }

    public async Task<List<StudentResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var students = await _studentRepository.GetAllAsync(cancellationToken);
        return students.Select(ToResponse).ToList();
    }

    public async Task<StudentResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var student = await GetOrThrowAsync(id, cancellationToken);
        return ToResponse(student);
    }

    public async Task<StudentResponse> CreateAsync(CreateStudentRequest request, CancellationToken cancellationToken)
    {
        if (await _studentRepository.ExistsByAdmNoAsync(request.AdmNo, cancellationToken))
            throw new ConflictException($"AdmNo '{request.AdmNo}' is already in use.");

        if (await _studentRepository.ExistsByRollNumberInClassAsync(request.ClassSectionId, request.RollNumber, cancellationToken))
            throw new ConflictException($"Roll number '{request.RollNumber}' is already in use in this class.");

        var classSection = await GetClassSectionOrThrowAsync(request.ClassSectionId, cancellationToken);

        var student = new Student
        {
            AdmNo = request.AdmNo,
            RollNumber = request.RollNumber,
            ClassSectionId = classSection.Id,
            AdmissionDate = request.AdmissionDate,
            Status = "Active",
            PhotoUrl = request.PhotoUrl,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            BloodGroup = request.BloodGroup,
            AadhaarNumber = request.AadhaarNumber,
            Mobile = request.Mobile,
            Email = request.Email,
            AddressLine = request.AddressLine,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            FatherName = request.FatherName,
            FatherOccupation = request.FatherOccupation,
            FatherMobile = request.FatherMobile,
            MotherName = request.MotherName,
            MotherOccupation = request.MotherOccupation,
            MotherMobile = request.MotherMobile,
            GuardianName = request.GuardianName,
            GuardianRelation = request.GuardianRelation,
            GuardianMobile = request.GuardianMobile,
            Category = request.Category,
            Religion = request.Religion,
            PreviousSchool = request.PreviousSchool,
            TransportRequired = request.TransportRequired,
            TransportRoute = request.TransportRoute,
            MedicalNotes = request.MedicalNotes,
            Nationality = request.Nationality,
            SecondNationality = request.SecondNationality,
            CountryOfBirth = request.CountryOfBirth,
            PreferredName = request.PreferredName,
            PassportNumber = request.PassportNumber,
            PassportExpiry = request.PassportExpiry,
            VisaType = request.VisaType,
            VisaExpiry = request.VisaExpiry,
            MotherTongue = request.MotherTongue,
            HomeLanguage = request.HomeLanguage,
            EnglishProficiency = request.EnglishProficiency,
            CurriculumTrack = request.CurriculumTrack,
            AdmissionType = request.AdmissionType,
            CustodyArrangement = request.CustodyArrangement,
            PrimaryContactParent = request.PrimaryContactParent,
            AuthorizedPickupPersons = request.AuthorizedPickupPersons,
            MediaConsent = request.MediaConsent,
            DietaryRequirements = request.DietaryRequirements,
            Allergies = request.Allergies,
            InsuranceProvider = request.InsuranceProvider,
            InsurancePolicyExpiry = request.InsurancePolicyExpiry,
            House = request.House,
            EalCode = request.EalCode,
            FeeConcessionPercent = request.FeeConcessionPercent,
            SpecialEducationalNeeds = request.SpecialEducationalNeeds,
        };

        await _studentRepository.AddAsync(student, cancellationToken);
        student.ClassSection = classSection;
        return ToResponse(student);
    }

    public async Task<StudentResponse> CreateFromAdmissionAsync(int admissionId, CreateStudentFromAdmissionRequest request, CancellationToken cancellationToken)
    {
        var admission = await _admissionRepository.GetByIdAsync(admissionId, cancellationToken)
            ?? throw new NotFoundException($"Admission {admissionId} does not exist.");

        if (admission.Status != "Enrolled")
            throw new ConflictException($"Cannot create a student from admission '{admission.RegNo}' — status is '{admission.Status}', expected 'Enrolled'.");

        if (admission.StudentId is not null)
            throw new ConflictException($"Admission '{admission.RegNo}' has already been converted to a student.");

        // Guaranteed non-null by the Enroll stage's own validation — asserting
        // here documents that dependency rather than silently trusting it.
        var classSectionId = admission.AllottedClassSectionId
            ?? throw new ConflictException($"Admission '{admission.RegNo}' is missing its allotted class section.");

        var student = new Student
        {
            AdmissionId = admission.Id,
            AdmNo = admission.AdmissionNumber!,
            RollNumber = admission.RollNumber!,
            ClassSectionId = classSectionId,
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

        student.ClassSection = await GetClassSectionOrThrowAsync(classSectionId, cancellationToken);
        student.Admission = admission;
        return ToResponse(student);
    }

    public async Task<StudentResponse> UpdateAsync(int id, UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var student = await GetOrThrowAsync(id, cancellationToken);
        var classSection = await GetClassSectionOrThrowAsync(request.ClassSectionId, cancellationToken);

        if (student.ClassSectionId != classSection.Id || student.RollNumber != request.RollNumber)
        {
            if (await _studentRepository.ExistsByRollNumberInClassAsync(classSection.Id, request.RollNumber, cancellationToken))
                throw new ConflictException($"Roll number '{request.RollNumber}' is already in use in this class.");
        }

        student.RollNumber = request.RollNumber;
        student.ClassSectionId = classSection.Id;
        student.Status = request.Status;
        student.PhotoUrl = request.PhotoUrl;
        student.FirstName = request.FirstName; student.MiddleName = request.MiddleName; student.LastName = request.LastName;
        student.Gender = request.Gender; student.DateOfBirth = request.DateOfBirth;
        student.BloodGroup = request.BloodGroup; student.AadhaarNumber = request.AadhaarNumber;
        student.Mobile = request.Mobile; student.Email = request.Email;
        student.AddressLine = request.AddressLine; student.City = request.City; student.State = request.State; student.Pincode = request.Pincode;
        student.FatherName = request.FatherName; student.FatherOccupation = request.FatherOccupation; student.FatherMobile = request.FatherMobile;
        student.MotherName = request.MotherName; student.MotherOccupation = request.MotherOccupation; student.MotherMobile = request.MotherMobile;
        student.GuardianName = request.GuardianName; student.GuardianRelation = request.GuardianRelation; student.GuardianMobile = request.GuardianMobile;
        student.Category = request.Category; student.Religion = request.Religion; student.PreviousSchool = request.PreviousSchool;
        student.TransportRequired = request.TransportRequired; student.TransportRoute = request.TransportRoute; student.MedicalNotes = request.MedicalNotes;
        student.Nationality = request.Nationality; student.SecondNationality = request.SecondNationality;
        student.CountryOfBirth = request.CountryOfBirth; student.PreferredName = request.PreferredName;
        student.PassportNumber = request.PassportNumber; student.PassportExpiry = request.PassportExpiry;
        student.VisaType = request.VisaType; student.VisaExpiry = request.VisaExpiry;
        student.MotherTongue = request.MotherTongue; student.HomeLanguage = request.HomeLanguage;
        student.EnglishProficiency = request.EnglishProficiency; student.CurriculumTrack = request.CurriculumTrack;
        student.AdmissionType = request.AdmissionType;
        student.CustodyArrangement = request.CustodyArrangement; student.PrimaryContactParent = request.PrimaryContactParent;
        student.AuthorizedPickupPersons = request.AuthorizedPickupPersons; student.MediaConsent = request.MediaConsent;
        student.DietaryRequirements = request.DietaryRequirements; student.Allergies = request.Allergies;
        student.InsuranceProvider = request.InsuranceProvider; student.InsurancePolicyExpiry = request.InsurancePolicyExpiry;
        student.House = request.House; student.EalCode = request.EalCode;
        student.FeeConcessionPercent = request.FeeConcessionPercent; student.SpecialEducationalNeeds = request.SpecialEducationalNeeds;

        await _studentRepository.SaveChangesAsync(cancellationToken);
        student.ClassSection = classSection;
        return ToResponse(student);
    }

    private async Task<Student> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _studentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Student {id} does not exist.");

    private async Task<ClassSection> GetClassSectionOrThrowAsync(int id, CancellationToken cancellationToken)
    {
        var sections = await _classSectionRepository.GetAllAsync(cancellationToken);
        return sections.FirstOrDefault(c => c.Id == id)
            ?? throw new NotFoundException($"Class section {id} does not exist.");
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var student = await GetOrThrowAsync(id, cancellationToken);
        student.IsDeleted = true;
        student.DeletedAt = DateTime.UtcNow;
        await _studentRepository.SaveChangesAsync(cancellationToken);
    }

    private static StudentResponse ToResponse(Student s) => new(
    s.Id, s.AdmNo, s.RollNumber, s.ClassSection.DisplayName, s.AdmissionDate, s.Status, s.PhotoUrl,
    s.FirstName, s.MiddleName, s.LastName, s.Gender, s.DateOfBirth, s.BloodGroup, s.AadhaarNumber,
    s.Mobile, s.Email, s.AddressLine, s.City, s.State, s.Pincode,
    s.FatherName, s.FatherOccupation, s.FatherMobile, s.MotherName, s.MotherOccupation, s.MotherMobile,
    s.GuardianName, s.GuardianRelation, s.GuardianMobile,
    s.Category, s.Religion, s.PreviousSchool, s.TransportRequired, s.TransportRoute, s.MedicalNotes,
    s.Nationality, s.SecondNationality, s.CountryOfBirth, s.PreferredName,
    s.PassportNumber, s.PassportExpiry, s.VisaType, s.VisaExpiry,
    s.MotherTongue, s.HomeLanguage, s.EnglishProficiency, s.CurriculumTrack, s.AdmissionType,
    s.CustodyArrangement, s.PrimaryContactParent, s.AuthorizedPickupPersons, s.MediaConsent,
    s.DietaryRequirements, s.Allergies, s.InsuranceProvider, s.InsurancePolicyExpiry,
    s.House, s.EalCode, s.FeeConcessionPercent, s.SpecialEducationalNeeds,
    s.Admission?.RegNo, s.AdmissionId);

}
