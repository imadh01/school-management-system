using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Interceptors;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Bug #3. Enrol copies values from the application into the student. Every destination
/// column must be at least as wide as its source, and both tables must use one vocabulary.
/// </summary>
public class ColumnWidthTests
{
    // Source column -> destination column, exactly the copies EnrollAsync performs.
    public static TheoryData<Type, string, Type, string> EnrolCopies => new()
    {
        { typeof(Admission), "FirstName",       typeof(Student), "FirstName" },
        { typeof(Admission), "MiddleName",      typeof(Student), "MiddleName" },
        { typeof(Admission), "LastName",        typeof(Student), "LastName" },
        { typeof(Admission), "Gender",          typeof(Student), "Gender" },
        { typeof(Admission), "AddressLine",     typeof(Student), "AddressLine" },
        { typeof(Admission), "City",            typeof(Student), "City" },
        { typeof(Admission), "State",           typeof(Student), "State" },
        { typeof(Admission), "Pincode",         typeof(Student), "Pincode" },
        { typeof(Admission), "Religion",        typeof(Student), "Religion" },
        { typeof(Admission), "Category",        typeof(Student), "Category" },
        { typeof(Admission), "PreviousSchool",  typeof(Student), "PreviousSchool" },
        { typeof(Admission), "AdmissionType",   typeof(Student), "AdmissionType" },
        { typeof(Admission), "RollNumber",      typeof(Student), "RollNumber" },
        { typeof(Admission), "RollNumber",      typeof(StudentEnrollment), "RollNumber" },
        { typeof(Admission), "AdmissionNumber", typeof(Student), "AdmNo" },
        { typeof(Admission), "BloodGroup",      typeof(StudentHealth), "BloodGroup" },
        { typeof(Admission), "MedicalNotes",    typeof(StudentHealth), "MedicalNotes" },
        { typeof(AdmissionGuardian), "Name",    typeof(Parent), "Name" },
        { typeof(AdmissionGuardian), "Mobile",  typeof(Parent), "Mobile" },
        { typeof(AdmissionGuardian), "Email",   typeof(Parent), "Email" },
    };

    [Theory]
    [MemberData(nameof(EnrolCopies))]
    public void Destination_column_is_not_narrower_than_its_source(
        Type sourceType, string sourceProperty, Type destinationType, string destinationProperty)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=.;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var db = new ApplicationDbContext(options, new AuditableEntitySaveChangesInterceptor());

        var source = db.Model.FindEntityType(sourceType)!.FindProperty(sourceProperty)!.GetMaxLength();
        var destination = db.Model.FindEntityType(destinationType)!.FindProperty(destinationProperty)!.GetMaxLength();

        // null = unlimited (nvarchar(max)).
        var fits = destination is null || (source is not null && destination >= source);
        Assert.True(fits,
            $"{sourceType.Name}.{sourceProperty} allows {source?.ToString() ?? "unlimited"} characters " +
            $"but {destinationType.Name}.{destinationProperty} only {destination?.ToString() ?? "unlimited"}.");
    }

    [Fact]
    public void Admission_and_student_use_the_same_default_admission_type()
    {
        Assert.Equal(new Admission().AdmissionType, new Student().AdmissionType);
    }
}

[Collection("Database")]
public class EnrolCopyBehaviourTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public EnrolCopyBehaviourTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Enrol_with_a_50_character_category_and_Transfer_type_copies_both_to_the_student()
    {
        var category = new string('c', 50);
        int admissionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db, capacity: 5);
            var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section, category, "Transfer");
            admissionId = admission.Id;

            var service = scope.ServiceProvider.GetRequiredService<IAdmissionService>();
            await service.EnrollAsync(admission.Id, TestData.EnrolRequest(section, guardian, "1"), CancellationToken.None);
        }

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var student = await verifyDb.Students.SingleAsync(s => s.AdmissionId == admissionId);
        Assert.Equal(category, student.Category);
        Assert.Equal("Transfer", student.AdmissionType);
    }

    [Fact]
    public async Task Enrol_with_no_category_gives_the_student_General()
    {
        int admissionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db, capacity: 5);
            var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section, category: null);
            admissionId = admission.Id;

            var service = scope.ServiceProvider.GetRequiredService<IAdmissionService>();
            await service.EnrollAsync(admission.Id, TestData.EnrolRequest(section, guardian, "1"), CancellationToken.None);
        }

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var student = await verifyDb.Students.SingleAsync(s => s.AdmissionId == admissionId);
        Assert.Equal("General", student.Category);
    }
}
