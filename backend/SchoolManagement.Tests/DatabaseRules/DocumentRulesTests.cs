using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// G1b: the document rules hold in the DATABASE, whatever code writes the rows: exactly one owner,
/// a rejection has a reason, a pending document has no reviewer,
/// and the same file once per owner (a deleted copy does not count). Plus the seeded types.
/// (The API tests in DocumentApiTests check the same rules through the service.)
/// </summary>
[Collection("Database")]
public class DocumentRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public DocumentRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task A_document_with_no_owner_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Documents.Add(await NewDocumentAsync(db, studentId: null));
        await AssertViolatesAsync(db, "CK_Documents_ExactlyOneOwner");
    }

    [Fact]
    public async Task A_rejection_needs_a_reason()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reviewer = await NewUserAsync(db);
        var document = await NewDocumentAsync(db, await NewStudentAsync(db));
        document.Status = DocumentStatuses.Rejected;
        document.VerifiedBy = reviewer.Id;
        document.VerifiedAt = DateTime.UtcNow;
        document.RejectionReason = "   ";

        db.Documents.Add(document);
        await AssertViolatesAsync(db, "CK_Documents_RejectedHasReason");
    }

    [Fact]
    public async Task A_pending_document_has_no_reviewer()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reviewer = await NewUserAsync(db);
        var document = await NewDocumentAsync(db, await NewStudentAsync(db));
        document.VerifiedBy = reviewer.Id; // still Pending

        db.Documents.Add(document);
        await AssertViolatesAsync(db, "CK_Documents_PendingHasNoReviewer");
    }

    [Fact]
    public async Task The_same_file_twice_for_one_owner_is_rejected_but_a_deleted_copy_does_not_count()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var studentId = await NewStudentAsync(db);
        var sha = RandomNumberGenerator.GetBytes(32);

        var first = await NewDocumentAsync(db, studentId, sha);
        db.Documents.Add(first);
        await db.SaveChangesAsync();

        db.Documents.Add(await NewDocumentAsync(db, studentId, sha));
        await AssertViolatesAsync(db, "UX_Documents_Student_Sha256");
        db.ChangeTracker.Clear();

        // Delete the first one: the same file may be uploaded again.
        var stored = await db.Documents.SingleAsync(d => d.Id == first.Id);
        stored.IsDeleted = true;
        await db.SaveChangesAsync();
        db.Documents.Add(await NewDocumentAsync(db, studentId, sha));
        await db.SaveChangesAsync(); // no exception
    }

    [Fact]
    public async Task The_same_file_for_two_different_students_is_allowed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sha = RandomNumberGenerator.GetBytes(32);

        db.Documents.Add(await NewDocumentAsync(db, await NewStudentAsync(db), sha));
        db.Documents.Add(await NewDocumentAsync(db, await NewStudentAsync(db), sha));
        await db.SaveChangesAsync(); // e.g. siblings sharing one family document
    }

    [Fact]
    public async Task The_seed_has_the_approved_types_with_their_rules()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var types = await db.DocumentTypes.ToDictionaryAsync(t => t.Code);

        Assert.Equal(10, types.Count);
        Assert.True(types["EmiratesId"].HasExpiry && types["EmiratesId"].IsSensitive);
        Assert.True(types["Passport"].HasExpiry);
        Assert.True(types["ResidenceVisa"].HasExpiry);
        Assert.True(types["Aadhaar"].IsSensitive && !types["Aadhaar"].HasExpiry);
        Assert.False(types["Photo"].IsSensitive);
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<int> NewStudentAsync(ApplicationDbContext db)
    {
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    private static async Task<User> NewUserAsync(ApplicationDbContext db)
    {
        var name = "doc_" + TestData.Unique();
        var user = new User { Username = name, Email = $"{name}@test.local", PasswordHash = "x", Status = "Active" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Document> NewDocumentAsync(ApplicationDbContext db, int? studentId, byte[]? sha = null) => new()
    {
        StudentId = studentId,
        DocumentTypeId = await db.DocumentTypes.Where(t => t.Code == "Photo").Select(t => t.Id).SingleAsync(),
        OriginalFileName = "photo.png",
        ContentType = DocumentContentTypes.Png,
        SizeBytes = 100,
        Sha256 = sha ?? RandomNumberGenerator.GetBytes(32),
        StorageKey = $"2026/10/{Guid.NewGuid():N}.png",
        Status = DocumentStatuses.Pending,
    };

    private static async Task AssertViolatesAsync(ApplicationDbContext db, string constraintOrIndex)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains(constraintOrIndex, sql.Message);
    }
}
