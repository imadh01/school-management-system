using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Tests.Support;

/// <summary>
/// Builds the minimum valid rows a test needs. Every call uses fresh unique values, so tests
/// sharing one database never collide on unique columns (AdmNo, RegNo, mobile, year name...).
/// </summary>
public static class TestData
{
    public static string Unique() => Guid.NewGuid().ToString("N")[..10];

    public static async Task<ClassSection> CreateSectionAsync(ApplicationDbContext db, int? capacity = null)
    {
        var year = new AcademicYear
        {
            Name = "AY" + Unique(),
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2027, 3, 31),
            IsCurrent = false,
        };
        var section = new ClassSection
        {
            AcademicYear = year,
            Name = "Class 1",
            Section = "A",
            Capacity = capacity,
            Status = "Active",
        };
        db.ClassSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    /// <summary>A second section in the SAME academic year as <paramref name="existing"/> (needed to test class moves).</summary>
    public static async Task<ClassSection> CreateSiblingSectionAsync(
        ApplicationDbContext db, ClassSection existing, string section, int? capacity = null)
    {
        var sibling = new ClassSection
        {
            AcademicYearId = existing.AcademicYearId,
            Name = existing.Name,
            Section = section,
            Capacity = capacity,
            Status = "Active",
        };
        db.ClassSections.Add(sibling);
        await db.SaveChangesAsync();
        return sibling;
    }

    /// <summary>An edit request that moves <paramref name="student"/> to another class and changes nothing else.</summary>
    public static UpdateStudentRequest MoveRequest(Student student, int targetClassSectionId, string rollNumber, string? rowVersion = null) =>
        new(RollNumber: rollNumber, ClassSectionId: targetClassSectionId, Status: "Active", PhotoUrl: null,
            FirstName: student.FirstName, MiddleName: null, LastName: student.LastName,
            Gender: student.Gender, DateOfBirth: student.DateOfBirth,
            Mobile: null, Email: null, AddressLine: null, City: null, State: null, Pincode: null,
            Category: "General", Religion: null, PreviousSchool: null,
            TransportRequired: false, TransportRoute: null,
            Nationality: null, SecondNationality: null, CountryOfBirth: null, PreferredName: null,
            MotherTongue: null, HomeLanguage: null, EnglishProficiency: null, CurriculumTrack: null,
            AdmissionType: "New", CustodyArrangement: null, MediaConsent: false,
            House: null, EalCode: null, FeeConcessionPercent: null,
            Health: null, PickupPersons: null,
            RowVersion: rowVersion ?? Convert.ToBase64String(student.RowVersion));

    /// <summary>A student with one enrollment row, written straight to the database (no service rules).</summary>
    public static Student NewStudent(ClassSection section, string rollNumber, string status = "Active")
    {
        var student = new Student
        {
            AdmNo = "T" + Unique(),
            RollNumber = rollNumber,
            ClassSectionId = section.Id,
            AdmissionDate = new DateOnly(2026, 4, 1),
            Status = status,
            FirstName = "Test",
            LastName = "Student" + Unique(),
            Gender = "Male",
            DateOfBirth = new DateOnly(2018, 1, 1),
        };

        var enrollment = StudentEnrollmentRules.StartNew(section, rollNumber, new DateOnly(2026, 4, 1));
        if (status != "Active")
        {
            enrollment.Status = EnrollmentStatuses.Left;
            enrollment.EndDate = new DateOnly(2026, 6, 1);
        }
        student.Enrollments.Add(enrollment);
        return student;
    }

    /// <summary>An application in the "Admitted" state with one guardian, ready to enrol.</summary>
    public static async Task<(Admission Admission, AdmissionGuardian Guardian)> CreateAdmittedAdmissionAsync(
        ApplicationDbContext db, ClassSection section, string? category = null, string admissionType = "New")
    {
        var guardian = new AdmissionGuardian
        {
            RelationType = "Father",
            Name = "Guardian " + Unique(),
            Mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            IsPrimaryContact = true,
        };
        var admission = new Admission
        {
            RegNo = "R" + Unique(),
            FirstName = "Test",
            LastName = "Applicant",
            Gender = "Female",
            DateOfBirth = new DateOnly(2018, 5, 5),
            AcademicYearId = section.AcademicYearId,
            AppliedForClassSectionId = section.Id,
            AdmissionType = admissionType,
            Phone = "9000000000",
            RegistrationDate = new DateOnly(2026, 3, 1),
            Status = "Admitted",
            Category = category,
            Guardians = { guardian },
        };
        db.Admissions.Add(admission);
        await db.SaveChangesAsync();
        return (admission, guardian);
    }

    public static EnrollAdmissionRequest EnrolRequest(
        ClassSection section, AdmissionGuardian guardian, string rollNumber) =>
        new(
            RollNumber: rollNumber,
            AdmissionNumber: "A" + Unique(),
            AdmissionDate: new DateOnly(2026, 6, 1),
            EntryPoint: null,
            TransportRequired: false,
            AllottedClassSectionId: section.Id,
            Nationality: null, CurriculumTrack: null, EnglishProficiency: null,
            EalCode: null, House: null, Allergies: null,
            Guardians: new List<GuardianDecision>
            {
                new(guardian.Id, GuardianActions.CreateNew, null),
            });
}