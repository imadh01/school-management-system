// G1b: unit tests for the document rules that need no database: the file-type check by content,
// the size message, the duplicate check, self-review and unverify-before-delete. The repository and
// the storage are mocked, so each test also proves what did NOT happen (e.g. nothing was stored).
// The same rules are tested end-to-end against SQL Server in DocumentApiTests.

using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SchoolManagement.Application.DTOs.Documents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class DocumentServiceTests
{
    private const int StudentId = 7;
    private const int UploaderId = 10;
    private const int ReviewerId = 20;

    private static readonly DateTime Now = new(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DocumentType Passport = new() { Id = 2, Code = "Passport", Name = "Passport", IsSensitive = true, HasExpiry = true };
    private static readonly DocumentType Photo = new() { Id = 6, Code = "Photo", Name = "Photograph" };

    // Smallest real headers of each accepted type, padded with a little content.
    public static readonly byte[] PdfBytes = "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();
    public static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 };
    public static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0 };
    /// <summary>A Windows program ("MZ" header), as if renamed to passport.pdf.</summary>
    public static readonly byte[] ExeBytes = { 0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0, 0xFF, 0xFF };

    private readonly Mock<IDocumentRepository> _repository = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<ICurrentUserPermissions> _permissions = new();
    private readonly DocumentSettings _settings = new() { MaxFileSizeBytes = 5 * 1024 * 1024, ExpiringSoonDays = 60 };
    private readonly DocumentService _sut;

    public DocumentServiceTests()
    {
        _currentUser.Setup(u => u.UserId).Returns(UploaderId);
        _repository.Setup(r => r.StudentExistsAsync(StudentId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.GetTypeAsync(Passport.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Passport);
        _repository.Setup(r => r.GetTypeAsync(Photo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Photo);
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("2026/10/0123456789abcdef0123456789abcdef.pdf");
        _repository.Setup(r => r.GetRowAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => Row(id));

        _sut = new DocumentService(
            _repository.Object, _storage.Object, _currentUser.Object, _permissions.Object,
            Mock.Of<ISensitiveReadLog>(), Mock.Of<IConcurrencyGuard>(), _settings,
            new FixedTimeProvider(Now), NullLogger<DocumentService>.Instance);
    }

    private static DocumentUpload File(byte[] bytes, string name, long? declaredLength = null) =>
        new(name, declaredLength ?? bytes.Length, new MemoryStream(bytes));

    private static UploadDocumentRequest PhotoRequest() => new() { DocumentTypeId = Photo.Id };

    private static DocumentRow Row(int id) => new(
        id, StudentId, null, "Test Student", "A1", Photo.Id, Photo.Name, false, false, null, "photo.png",
        DocumentContentTypes.Png, 16, null, DocumentStatuses.Pending, UploaderId, "clerk", Now, null, null, null,
        new byte[8]);

    private void NothingWasStored() =>
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    // ─────────────────────────────────────────────────────────────────
    // File type: decided by the content
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void The_type_is_read_from_the_first_bytes()
    {
        Assert.Equal(DocumentContentTypes.Pdf, FileSignatures.Detect(PdfBytes));
        Assert.Equal(DocumentContentTypes.Png, FileSignatures.Detect(PngBytes));
        Assert.Equal(DocumentContentTypes.Jpeg, FileSignatures.Detect(JpegBytes));
        Assert.Null(FileSignatures.Detect(ExeBytes));
        Assert.Null(FileSignatures.Detect("just some text"u8));
        Assert.Null(FileSignatures.Detect(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public async Task A_program_renamed_to_pdf_is_refused_and_nothing_is_stored()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _sut.UploadForStudentAsync(StudentId, PhotoRequest(), File(ExeBytes, "passport.pdf"), default));

        Assert.Contains("PDF, JPG and PNG", ex.Errors["File"][0]);
        NothingWasStored();
        _repository.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_real_png_named_pdf_is_stored_as_a_png()
    {
        Document? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((d, _) => saved = d);

        await _sut.UploadForStudentAsync(StudentId, PhotoRequest(), File(PngBytes, "scan.pdf"), default);

        Assert.Equal(DocumentContentTypes.Png, saved!.ContentType);
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), ".png", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("scan.png", FileSignatures.DownloadName(saved.OriginalFileName, saved.ContentType));
    }

    // ─────────────────────────────────────────────────────────────────
    // Size: a setting, and the message shows both numbers
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_oversized_file_is_refused_with_its_exact_size_and_the_limit()
    {
        var oneByteOver = _settings.MaxFileSizeBytes + 1; // 5,242,881

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _sut.UploadForStudentAsync(StudentId, PhotoRequest(), File(PdfBytes, "big.pdf", oneByteOver), default));

        var message = ex.Errors["File"][0];
        Assert.Contains("5,242,881 bytes", message);
        Assert.Contains("5,242,880 bytes", message);
        NothingWasStored();
    }

    [Fact]
    public async Task The_limit_comes_from_the_setting_not_a_constant()
    {
        _settings.MaxFileSizeBytes = 10; // a school that lowered the limit

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _sut.UploadForStudentAsync(StudentId, PhotoRequest(), File(PngBytes, "photo.png"), default));

        Assert.Contains("16 bytes", ex.Errors["File"][0]);
        Assert.Contains("10 bytes", ex.Errors["File"][0]);
    }

    [Fact]
    public void Sizes_read_well_and_stay_exact()
    {
        Assert.Equal("5 MB (5,242,880 bytes)", FileSignatures.DescribeSize(5_242_880));
        Assert.Equal("5.31 MB (5,567,201 bytes)", FileSignatures.DescribeSize(5_567_201));
        Assert.Equal("245 KB (250,880 bytes)", FileSignatures.DescribeSize(250_880));
    }

    // ─────────────────────────────────────────────────────────────────
    // Other upload rules
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_type_with_expiry_needs_an_expiry_date()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _sut.UploadForStudentAsync(StudentId, new UploadDocumentRequest { DocumentTypeId = Passport.Id },
                File(PdfBytes, "passport.pdf"), default));

        Assert.True(ex.Errors.ContainsKey("ExpiryDate"));
        NothingWasStored();
    }

    [Fact]
    public async Task The_same_file_twice_for_one_student_is_a_conflict_and_is_not_stored_again()
    {
        var sha = SHA256.HashData(PngBytes);
        _repository.Setup(r => r.FindDuplicateAsync(StudentId, null, It.Is<byte[]>(b => b.SequenceEqual(sha)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DuplicateDocument(1, "Photograph", DocumentStatuses.Pending, Now));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UploadForStudentAsync(StudentId, PhotoRequest(), File(PngBytes, "photo again.png"), default));

        Assert.Contains("already attached", ex.Message);
        NothingWasStored();
    }

    [Fact]
    public async Task When_the_insert_fails_the_stored_file_is_removed()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("duplicate"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UploadForStudentAsync(StudentId, PhotoRequest(), File(PngBytes, "photo.png"), default));

        _storage.Verify(s => s.DeleteAsync("2026/10/0123456789abcdef0123456789abcdef.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_enrolled_application_takes_no_new_documents()
    {
        _repository.Setup(r => r.GetAdmissionStatusAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(AdmissionStatuses.Enrolled);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UploadForAdmissionAsync(3, PhotoRequest(), File(PngBytes, "photo.png"), default));
        NothingWasStored();
    }

    [Theory]
    [InlineData(@"C:\fakepath\passport.pdf", "passport.pdf")]
    [InlineData("../../web.config", "web.config")]
    [InlineData("   ", "document")]
    [InlineData("a\u0000b.png", "ab.png")]
    public void File_names_are_kept_as_labels_only(string sent, string kept) =>
        Assert.Equal(kept, DocumentService.CleanFileName(sent));

    // ─────────────────────────────────────────────────────────────────
    // Review and delete
    // ─────────────────────────────────────────────────────────────────

    private Document Stored(string status, int? uploadedBy = UploaderId)
    {
        var document = new Document
        {
            Id = 99, StudentId = StudentId, DocumentTypeId = Photo.Id, DocumentType = Photo, Status = status,
            CreatedBy = uploadedBy, RowVersion = new byte[8],
            VerifiedBy = status == DocumentStatuses.Pending ? null : ReviewerId,
            VerifiedAt = status == DocumentStatuses.Pending ? null : Now,
        };
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        return document;
    }

    private static readonly ReviewDocumentRequest Review = new(Convert.ToBase64String(new byte[8]));

    [Fact]
    public async Task The_uploader_cannot_verify_their_own_document()
    {
        var document = Stored(DocumentStatuses.Pending, uploadedBy: UploaderId); // the caller is the uploader

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.VerifyAsync(99, Review, default));

        Assert.Contains("someone else", ex.Message);
        Assert.Equal(DocumentStatuses.Pending, document.Status);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_uploader_cannot_reject_their_own_document_either()
    {
        Stored(DocumentStatuses.Pending, uploadedBy: UploaderId);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.RejectAsync(99, new RejectDocumentRequest("Blurred", Review.RowVersion), default));
    }

    [Fact]
    public async Task Someone_else_can_verify_and_is_recorded_as_the_reviewer()
    {
        var document = Stored(DocumentStatuses.Pending, uploadedBy: ReviewerId + 1);

        await _sut.VerifyAsync(99, Review, default);

        Assert.Equal(DocumentStatuses.Verified, document.Status);
        Assert.Equal(UploaderId, document.VerifiedBy); // the caller
        Assert.Equal(Now, document.VerifiedAt);
    }

    [Fact]
    public async Task A_verified_document_cannot_be_deleted_until_it_is_unverified()
    {
        var document = Stored(DocumentStatuses.Verified, uploadedBy: ReviewerId + 1);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(99, default));
        Assert.Contains("Unverify it first", ex.Message);
        Assert.False(document.IsDeleted);

        await _sut.UnverifyAsync(99, Review, default);
        Assert.Equal(DocumentStatuses.Pending, document.Status);
        Assert.Null(document.VerifiedBy);
        Assert.Null(document.VerifiedAt);

        await _sut.DeleteAsync(99, default);
        Assert.True(document.IsDeleted);
        Assert.Equal(Now, document.DeletedAt);
        // Soft delete keeps the file until the purge.
        _storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Only_a_verified_document_can_be_unverified()
    {
        Stored(DocumentStatuses.Rejected, uploadedBy: ReviewerId + 1);
        await Assert.ThrowsAsync<ConflictException>(() => _sut.UnverifyAsync(99, Review, default));
    }

    // ─────────────────────────────────────────────────────────────────
    // Expiry flag and purge
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Expiry_state_is_expired_before_today_and_expiring_soon_within_the_window()
    {
        var today = new DateOnly(2026, 10, 13);
        Assert.Null(DocumentService.ExpiryStateOf(null, today, 60));
        Assert.Equal(DocumentExpiryStates.Expired, DocumentService.ExpiryStateOf(today.AddDays(-1), today, 60));
        Assert.Equal(DocumentExpiryStates.ExpiringSoon, DocumentService.ExpiryStateOf(today, today, 60));
        Assert.Equal(DocumentExpiryStates.ExpiringSoon, DocumentService.ExpiryStateOf(today.AddDays(60), today, 60));
        Assert.Null(DocumentService.ExpiryStateOf(today.AddDays(61), today, 60));
    }

    [Fact]
    public async Task The_purge_removes_files_older_than_the_retention_and_marks_the_rows()
    {
        _settings.DeletedFileRetentionDays = 90;
        var old = new Document { Id = 1, StorageKey = "2026/01/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf", IsDeleted = true };
        _repository.Setup(r => r.GetPurgeBatchAsync(Now.AddDays(-90), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document> { old });

        var purged = await _sut.PurgeDeletedFilesAsync(default);

        Assert.Equal(1, purged);
        Assert.Equal(Now, old.FilePurgedAt);
        _storage.Verify(s => s.DeleteAsync(old.StorageKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
