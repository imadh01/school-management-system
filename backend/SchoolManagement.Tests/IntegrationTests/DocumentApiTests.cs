// G1b: the document rules end-to-end, through the real API, SQL Server and file storage:
//   - an oversized file is refused with its exact size and the limit;
//   - a renamed file whose content is not PDF/JPG/PNG is refused;
//   - the same file uploaded twice gives one document, not two;
//   - an Admin may verify their own upload;
//   - a verified document must be unverified before it can be deleted;
// plus the download (same bytes, Read audit row, sensitive files need Students.ViewSensitive) and the
// move of an applicant's documents to the student at enrolment.
//
// Two users act: the "uploader" and the "reviewer". Both hold the Admin role (every permission), so
// the only thing that differs between them is WHO uploaded the document.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using SchoolManagement.Tests.UnitTests;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class DocumentApiTests
{
    private const string Password = "Docs@12345";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────────
    // The five rules
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_oversized_file_is_refused_with_its_exact_size_and_the_limit()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var studentId = await CreateStudentAsync();

        // A real PDF header, one byte over the 5 MB default limit.
        var bytes = new byte[5 * 1024 * 1024 + 1];
        DocumentServiceTests.PdfBytes.CopyTo(bytes, 0);

        var response = await UploadAsync(uploader, studentId, "Photo", bytes, "big.pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await FieldBodyAsync(response, "File");
        Assert.Contains("5,242,881 bytes", error);
        Assert.Contains("5,242,880 bytes", error);
        Assert.Equal(0, await CountDocumentsAsync(studentId));
    }

    [Fact]
    public async Task A_renamed_file_whose_content_is_not_pdf_jpg_or_png_is_refused()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var studentId = await CreateStudentAsync();

        // A Windows program named passport.pdf and sent as application/pdf: name and browser type both lie.
        var response = await UploadAsync(uploader, studentId, "Photo", DocumentServiceTests.ExeBytes, "passport.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PDF, JPG and PNG", await FieldBodyAsync(response, "File"));
        Assert.Equal(0, await CountDocumentsAsync(studentId));
    }

    [Fact]
    public async Task Uploading_the_same_file_twice_keeps_one_document()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var studentId = await CreateStudentAsync();
        var bytes = UniquePng();

        var first = await UploadAsync(uploader, studentId, "Photo", bytes, "photo.png");
        var second = await UploadAsync(uploader, studentId, "Photo", bytes, "photo (1).png");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already attached", (await BodyAsync(second)).GetProperty("message").GetString());
        Assert.Equal(1, await CountDocumentsAsync(studentId));
    }

    [Fact]
    public async Task An_Admin_can_verify_their_own_upload_and_someone_else_can_too()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var reviewer = await CreateUserAsync(RoleNames.Admin);
        var studentId = await CreateStudentAsync();
        var own = await UploadOkAsync(uploader, studentId, "Photo", UniquePng(), "photo.png");
        var other = await UploadOkAsync(uploader, studentId, "Photo", UniquePng(), "photo2.png");

        // An Admin may review their own upload (decision of 16 Oct); the same rule still stops everyone else.
        var ownReview = await PostAsync(uploader, $"/api/documents/{own.Id}/verify", new { rowVersion = own.RowVersion });
        Assert.Equal(HttpStatusCode.OK, ownReview.StatusCode);
        Assert.Equal("Verified", (await ReadAsync<DocumentDto>(ownReview)).Status);

        var byOther = await PostAsync(reviewer, $"/api/documents/{other.Id}/verify", new { rowVersion = other.RowVersion });
        Assert.Equal(HttpStatusCode.OK, byOther.StatusCode);
        var verified = await ReadAsync<DocumentDto>(byOther);
        Assert.Equal("Verified", verified.Status);
        Assert.Equal(reviewer.Username, verified.ReviewedBy);
    }

    [Fact]
    public async Task A_verified_document_must_be_unverified_before_it_is_deleted()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var reviewer = await CreateUserAsync(RoleNames.Admin);
        var studentId = await CreateStudentAsync();
        var document = await UploadOkAsync(uploader, studentId, "Photo", UniquePng(), "photo.png");
        var verified = await ReadAsync<DocumentDto>(
            await PostAsync(reviewer, $"/api/documents/{document.Id}/verify", new { rowVersion = document.RowVersion }));

        var blocked = await SendAsync(reviewer, HttpMethod.Delete, $"/api/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("Unverify it first", (await BodyAsync(blocked)).GetProperty("message").GetString());

        var unverify = await PostAsync(reviewer, $"/api/documents/{document.Id}/unverify", new { rowVersion = verified.RowVersion });
        Assert.Equal(HttpStatusCode.OK, unverify.StatusCode);
        Assert.Equal("Pending", (await ReadAsync<DocumentDto>(unverify)).Status);

        var deleted = await SendAsync(reviewer, HttpMethod.Delete, $"/api/documents/{document.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, await CountDocumentsAsync(studentId));

        // Soft delete: the row and the file stay until the purge; the unverify has its own audit row.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Documents.IgnoreQueryFilters().SingleAsync(d => d.Id == document.Id);
        Assert.True(row.IsDeleted);
        Assert.True(File.Exists(Path.Combine(_factory.DocumentStorageRoot, row.StorageKey)));
        var statusChanges = await db.AuditLogs
            .Where(a => a.TableName == "Documents" && a.RecordId == document.Id.ToString() && a.Action == "Update")
            .Select(a => a.Changes!)
            .ToListAsync();
        Assert.Contains(statusChanges, c => c.Contains("\"old\":\"Verified\"") && c.Contains("\"new\":\"Pending\""));
    }

    // ─────────────────────────────────────────────────────────────────
    // Download and enrolment
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Download_returns_the_same_bytes_and_leaves_a_Read_audit_row()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var studentId = await CreateStudentAsync();
        var bytes = UniquePng();
        var document = await UploadOkAsync(uploader, studentId, "Photo", bytes, "scan.pdf"); // a PNG named .pdf

        var response = await SendAsync(uploader, HttpMethod.Get, $"/api/documents/{document.Id}/file");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("scan.png", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.TableName == "Documents" && a.RecordId == document.Id.ToString() && a.Action == "Read"));
    }

    [Fact]
    public async Task A_sensitive_file_needs_Students_ViewSensitive_to_download()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        var viewer = await CreateUserAsync(await CreateRoleAsync(Permissions.DocumentsView));
        var studentId = await CreateStudentAsync();
        var birthCertificate = await UploadOkAsync(uploader, studentId, "BirthCertificate", UniquePng(), "bc.png");
        var photo = await UploadOkAsync(uploader, studentId, "Photo", UniquePng(), "photo.png");

        var list = await ReadAsync<List<DocumentDto>>(await SendAsync(viewer, HttpMethod.Get, $"/api/students/{studentId}/documents"));
        Assert.False(list.Single(d => d.Id == birthCertificate.Id).CanDownload);
        Assert.True(list.Single(d => d.Id == photo.Id).CanDownload);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(viewer, HttpMethod.Get, $"/api/documents/{birthCertificate.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(viewer, HttpMethod.Get, $"/api/documents/{photo.Id}/file")).StatusCode);
    }

    [Fact]
    public async Task Enrolment_moves_the_application_documents_to_the_student()
    {
        var uploader = await CreateUserAsync(RoleNames.Admin);
        int admissionId;
        object enrolRequest;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var section = await TestData.CreateSectionAsync(db);
            var (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section);
            admissionId = admission.Id;
            enrolRequest = TestData.EnrolRequest(section, guardian, "1");
        }

        var document = await UploadOkAsync(uploader, null, "Photo", UniquePng(), "photo.png", admissionId);

        var enrol = await PostAsync(uploader, $"/api/admissions/{admissionId}/enroll", enrolRequest);
        Assert.Equal(HttpStatusCode.OK, enrol.StatusCode);
        var studentId = (await BodyAsync(enrol)).GetProperty("studentId").GetInt32();

        var onApplication = await ReadAsync<List<DocumentDto>>(
            await SendAsync(uploader, HttpMethod.Get, $"/api/admissions/{admissionId}/documents"));
        var onStudent = await ReadAsync<List<DocumentDto>>(
            await SendAsync(uploader, HttpMethod.Get, $"/api/students/{studentId}/documents"));
        Assert.Empty(onApplication);
        Assert.Equal(document.Id, Assert.Single(onStudent).Id);

        // New uploads to the enrolled application are sent to the student's page.
        var late = await UploadAsync(uploader, null, "Photo", UniquePng(), "late.png", admissionId);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private sealed record Session(int UserId, string Username, string Token);

    /// <summary>The fields of DocumentResponse these tests read.</summary>
    private sealed record DocumentDto(int Id, string Status, string? ReviewedBy, bool CanDownload, string RowVersion);

    /// <summary>A valid PNG header followed by random bytes: a different file (and SHA-256) every call.</summary>
    private static byte[] UniquePng()
    {
        var bytes = new byte[64];
        Random.Shared.NextBytes(bytes);
        DocumentServiceTests.PngBytes.CopyTo(bytes, 0);
        return bytes;
    }

    private async Task<int> CreateStudentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    private async Task<string> CreateRoleAsync(params string[] permissionNames)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = new Role { Name = "docs_" + TestData.Unique(), IsActive = true };
        foreach (var permission in await db.Permissions.Where(p => permissionNames.Contains(p.Name)).ToListAsync())
            role.RolePermissions.Add(new RolePermission { Permission = permission });
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Name;
    }

    private async Task<Session> CreateUserAsync(string roleName)
    {
        var username = "docs_" + TestData.Unique();
        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var role = await db.Roles.SingleAsync(r => r.Name == roleName);
            var user = new User
            {
                Username = username,
                Email = $"{username}@test.local",
                PasswordHash = hasher.HashPassword(Password),
                Status = "Active",
                SecurityStamp = Guid.NewGuid(),
            };
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, Password));
        login.EnsureSuccessStatusCode();
        return new Session(userId, username, (await login.Content.ReadFromJsonAsync<AuthResult>())!.Token);
    }

    private async Task<int> TypeIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.DocumentTypes.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
    }

    private async Task<HttpResponseMessage> UploadAsync(
        Session session, int? studentId, string typeCode, byte[] bytes, string fileName,
        string browserContentType = "application/octet-stream", int? admissionId = null)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(browserContentType);
        var form = new MultipartFormDataContent
        {
            { new StringContent((await TypeIdAsync(typeCode)).ToString()), "documentTypeId" },
            { file, "file", fileName },
        };

        var url = studentId is not null ? $"/api/students/{studentId}/documents" : $"/api/admissions/{admissionId}/documents";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> UploadAsync(
        Session session, int? studentId, string typeCode, byte[] bytes, string fileName, int admissionId) =>
        UploadAsync(session, studentId, typeCode, bytes, fileName, "application/octet-stream", admissionId);

    private async Task<DocumentDto> UploadOkAsync(
        Session session, int? studentId, string typeCode, byte[] bytes, string fileName, int? admissionId = null)
    {
        var response = await UploadAsync(session, studentId, typeCode, bytes, fileName, "application/octet-stream", admissionId);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await ReadAsync<DocumentDto>(response);
    }

    private async Task<HttpResponseMessage> SendAsync(Session session, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAsync(Session session, string url, object body) =>
        SendAsync(session, HttpMethod.Post, url, body);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<string> FieldBodyAsync(HttpResponseMessage response, string field)
    {
        var body = await BodyAsync(response);
        Assert.True(body.TryGetProperty("errors", out var errors), body.ToString());
        return errors.GetProperty(field)[0].GetString()!;
    }

    private async Task<int> CountDocumentsAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Documents.CountAsync(d => d.StudentId == studentId);
    }
}
