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
/// The audit trail: every save stamps CreatedAt/UpdatedAt/CreatedBy/UpdatedBy and writes one AuditLogs row
/// per created / changed / deleted record - without leaking sensitive values into the log.
/// </summary>
[Collection("Database")]
public class AuditTrailTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AuditTrailTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task A_new_record_gets_its_timestamps_and_an_insert_log_row()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var section = await TestData.CreateSectionAsync(db, capacity: 30);

        Assert.True(section.CreatedAt > new DateTime(2020, 1, 1), "CreatedAt was never filled in.");
        Assert.Equal(section.CreatedAt, section.UpdatedAt);

        var log = await db.AuditLogs.SingleAsync(l =>
            l.TableName == "ClassSections" && l.RecordId == section.Id.ToString() && l.Action == AuditActions.Insert);
        Assert.Equal("system", log.UserName); // no logged-in user in a test
        Assert.Contains("\"capacity\"", log.Changes!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_edit_logs_only_the_fields_that_changed_with_old_and_new_values()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db, capacity: 30);

        section.Capacity = 40;
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.SingleAsync(l =>
            l.TableName == "ClassSections" && l.RecordId == section.Id.ToString() && l.Action == AuditActions.Update);
        Assert.Contains("\"old\":30", log.Changes!);
        Assert.Contains("\"new\":40", log.Changes!);
        Assert.DoesNotContain("Section", log.Changes!); // untouched fields stay out of the log
    }

    [Fact]
    public async Task A_save_that_changes_nothing_writes_no_log_row()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db, capacity: 30);

        section.Capacity = 30; // same value again
        await db.SaveChangesAsync();

        var updates = await db.AuditLogs.CountAsync(l =>
            l.TableName == "ClassSections" && l.RecordId == section.Id.ToString() && l.Action == AuditActions.Update);
        Assert.Equal(0, updates);
    }

    [Fact]
    public async Task A_soft_delete_is_logged_as_a_delete()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);

        section.IsDeleted = true;
        await db.SaveChangesAsync();

        Assert.NotNull(section.DeletedAt); // filled in by the interceptor
        Assert.True(await db.AuditLogs.AnyAsync(l =>
            l.TableName == "ClassSections" && l.RecordId == section.Id.ToString() && l.Action == AuditActions.Delete));
    }

    [Fact]
    public async Task Sensitive_values_never_reach_the_audit_log()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var section = await TestData.CreateSectionAsync(db);

        var aadhaar = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
        var student = TestData.NewStudent(section, "1");
        student.IdentityDocument = new StudentIdentityDocument { AadhaarNumber = aadhaar };
        student.Health = new StudentHealth { Allergies = "peanuts-secret" };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var identityLog = await db.AuditLogs.SingleAsync(l =>
            l.TableName == "StudentIdentityDocuments" && l.RecordId == student.Id.ToString());
        Assert.DoesNotContain(aadhaar, identityLog.Changes!);
        Assert.Contains("***", identityLog.Changes!);

        var healthLog = await db.AuditLogs.SingleAsync(l =>
            l.TableName == "StudentHealth" && l.RecordId == student.Id.ToString());
        Assert.DoesNotContain("peanuts-secret", healthLog.Changes!);
    }

    [Fact]
    public async Task The_logged_in_user_is_recorded_in_the_columns_and_in_the_log()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        var interceptor = new AuditableEntitySaveChangesInterceptor(new FakeCurrentUser(7, "imadh", "trace-123"));
        using var db = new ApplicationDbContext(options, interceptor);

        var section = await TestData.CreateSectionAsync(db);

        Assert.Equal(7, section.CreatedBy);
        Assert.Equal(7, section.UpdatedBy);
        var log = await db.AuditLogs.SingleAsync(l =>
            l.TableName == "ClassSections" && l.RecordId == section.Id.ToString());
        Assert.Equal(7, log.UserId);
        Assert.Equal("imadh", log.UserName);
        Assert.Equal("trace-123", log.TraceId);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(int userId, string userName, string traceId)
        {
            UserId = userId;
            UserName = userName;
            TraceId = traceId;
        }

        public int? UserId { get; }
        public string? UserName { get; }
        public string? TraceId { get; }
    }
}
