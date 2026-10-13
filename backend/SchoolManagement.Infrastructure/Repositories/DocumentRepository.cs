using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly ApplicationDbContext _context;

    public DocumentRepository(ApplicationDbContext context) => _context = context;

    // Ten-odd rows of reference data: one query, filtered in memory by the service.
    public Task<List<DocumentType>> GetTypesAsync(CancellationToken cancellationToken) =>
        _context.DocumentTypes.AsNoTracking().ToListAsync(cancellationToken);

    public Task<DocumentType?> GetTypeAsync(int id, CancellationToken cancellationToken) =>
        _context.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Document?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        VisibleDocuments().Include(d => d.DocumentType).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<DocumentRow?> GetRowAsync(int id, CancellationToken cancellationToken) =>
        Rows(VisibleDocuments().Where(d => d.Id == id)).FirstOrDefaultAsync(cancellationToken);

    public Task<List<DocumentRow>> ListAsync(DocumentFilter filter, CancellationToken cancellationToken)
    {
        var query = VisibleDocuments();

        if (filter.StudentId is { } studentId) query = query.Where(d => d.StudentId == studentId);
        if (filter.AdmissionId is { } admissionId) query = query.Where(d => d.AdmissionId == admissionId);
        if (filter.Status is { } status) query = query.Where(d => d.Status == status);
        if (filter.DocumentTypeId is { } typeId) query = query.Where(d => d.DocumentTypeId == typeId);
        if (filter.ExpiresOnOrBefore is { } before) query = query.Where(d => d.ExpiryDate != null && d.ExpiryDate <= before);
        if (filter.ExpiresOnOrAfter is { } after) query = query.Where(d => d.ExpiryDate != null && d.ExpiryDate >= after);

        // Newest first: the order of the Documents page and of the Documents tabs.
        return Rows(query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)).ToListAsync(cancellationToken);
    }

    public Task<bool> StudentExistsAsync(int studentId, CancellationToken cancellationToken) =>
        _context.Students.AnyAsync(s => s.Id == studentId, cancellationToken);

    public Task<string?> GetAdmissionStatusAsync(int admissionId, CancellationToken cancellationToken) =>
        _context.Admissions.Where(a => a.Id == admissionId).Select(a => (string?)a.Status).FirstOrDefaultAsync(cancellationToken);

    public Task<DuplicateDocument?> FindDuplicateAsync(
        int? studentId, int? admissionId, byte[] sha256, CancellationToken cancellationToken) =>
        _context.Documents
            .Where(d => d.Sha256 == sha256 &&
                        (studentId != null ? d.StudentId == studentId : d.AdmissionId == admissionId))
            .Select(d => new DuplicateDocument(d.Id, d.DocumentType.Name, d.Status, d.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        _context.Documents.Add(document);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        // The service looked for a duplicate first; two uploads of the same file at the same moment
        // both pass that check, and the unique index stops the second one here.
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql
                                           && sql.Message.Contains("_Sha256"))
        {
            _context.Entry(document).State = EntityState.Detached;
            throw new ConflictException("This file is already attached to this record. The same file is not stored twice.");
        }
    }

    public Task<List<Document>> GetForAdmissionAsync(int admissionId, CancellationToken cancellationToken) =>
        _context.Documents.Where(d => d.AdmissionId == admissionId).ToListAsync(cancellationToken);

    public Task<List<Document>> GetPurgeBatchAsync(DateTime deletedBefore, int take, CancellationToken cancellationToken) =>
        _context.Documents
            .IgnoreQueryFilters() // the deleted ones are exactly what we want here
            .Where(d => d.IsDeleted && d.FilePurgedAt == null && d.DeletedAt != null && d.DeletedAt < deletedBefore)
            .OrderBy(d => d.DeletedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Not-deleted documents whose owner is not deleted either: a deleted student's documents disappear
    /// with the student.
    ///
    /// The IsDeleted checks are written out on purpose, although query filters exist: <see cref="Rows"/>
    /// uses IgnoreQueryFilters to name deleted users, and EF Core then switches off EVERY filter in
    /// the query, including these.
    /// </summary>
    private IQueryable<Document> VisibleDocuments() =>
        _context.Documents.Where(d => !d.IsDeleted &&
            ((d.StudentId != null && !d.Student!.IsDeleted) || (d.AdmissionId != null && !d.Admission!.IsDeleted)));

    /// <summary>The list projection: one SQL query with the names joined in, never the file.</summary>
    private IQueryable<DocumentRow> Rows(IQueryable<Document> documents) =>
        documents.Select(d => new DocumentRow(
            d.Id,
            d.StudentId,
            d.AdmissionId,
            d.StudentId != null ? d.Student!.FirstName + " " + d.Student.LastName : d.Admission!.FirstName + " " + d.Admission.LastName,
            d.StudentId != null ? d.Student!.AdmNo : d.Admission!.RegNo,
            d.DocumentTypeId,
            d.DocumentType.Name,
            d.DocumentType.IsSensitive,
            d.DocumentType.HasExpiry,
            d.Title,
            d.OriginalFileName,
            d.ContentType,
            d.SizeBytes,
            d.ExpiryDate,
            d.Status,
            d.CreatedBy,
            // A user deleted later is still named: the history must not lose who uploaded what.
            _context.Users.IgnoreQueryFilters().Where(u => u.Id == d.CreatedBy).Select(u => u.Username).FirstOrDefault(),
            d.CreatedAt,
            _context.Users.IgnoreQueryFilters().Where(u => u.Id == d.VerifiedBy).Select(u => u.Username).FirstOrDefault(),
            d.VerifiedAt,
            d.RejectionReason,
            d.RowVersion));
}
