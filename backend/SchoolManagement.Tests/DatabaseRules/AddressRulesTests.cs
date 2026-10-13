using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// G1a: the address rules hold in the DATABASE, whatever code writes the rows:
/// exactly one owner, at most one address per type per owner, at most one primary per owner,
/// and a region that belongs to the address's country. Then the service rules that need the
/// Countries table (region required, postcode per country), the replace-by-type save, and the
/// copy at enrolment.
/// </summary>
[Collection("Database")]
public class AddressRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public AddressRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ database constraints

    [Fact]
    public async Task An_address_with_no_owner_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");

        db.Addresses.Add(NewAddress(uae, AddressTypes.Home));
        await AssertViolatesAsync(db, "CK_Addresses_ExactlyOneOwner");
    }

    [Fact]
    public async Task An_address_with_two_owners_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var parent = await NewParentAsync(db);
        var section = await TestData.CreateSectionAsync(db);
        var student = TestData.NewStudent(section, "1");
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var address = NewAddress(uae, AddressTypes.Home);
        address.ParentId = parent.Id;
        address.StudentId = student.Id;
        db.Addresses.Add(address);
        await AssertViolatesAsync(db, "CK_Addresses_ExactlyOneOwner");
    }

    [Fact]
    public async Task Two_addresses_of_the_same_type_for_one_owner_are_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var parent = await NewParentAsync(db);

        parent.Addresses.Add(NewAddress(uae, AddressTypes.Home, primary: true));
        await db.SaveChangesAsync();
        parent.Addresses.Add(NewAddress(uae, AddressTypes.Home));
        await AssertViolatesAsync(db, "UX_Addresses_Parent_Type");
    }

    [Fact]
    public async Task Two_primary_addresses_for_one_owner_are_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var parent = await NewParentAsync(db);

        parent.Addresses.Add(NewAddress(uae, AddressTypes.Home, primary: true));
        await db.SaveChangesAsync();
        parent.Addresses.Add(NewAddress(uae, AddressTypes.Mailing, primary: true));
        await AssertViolatesAsync(db, "UX_Addresses_Parent_Primary");
    }

    [Fact]
    public async Task The_same_type_for_two_different_owners_is_allowed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var first = await NewParentAsync(db);
        var second = await NewParentAsync(db);

        first.Addresses.Add(NewAddress(uae, AddressTypes.Home, primary: true));
        second.Addresses.Add(NewAddress(uae, AddressTypes.Home, primary: true));
        await db.SaveChangesAsync(); // no exception
    }

    [Fact]
    public async Task A_region_of_another_country_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var india = await CountryAsync(db, "IN");
        var parent = await NewParentAsync(db);

        var address = NewAddress(uae, AddressTypes.Home, primary: true);
        address.RegionId = india.Regions.First().Id; // an Indian state on a UAE address
        parent.Addresses.Add(address);
        await AssertViolatesAsync(db, "FK_Addresses_CountryRegions_CountryId_RegionId");
    }

    [Fact]
    public async Task The_seed_has_the_seven_emirates_and_the_indian_states()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var india = await CountryAsync(db, "IN");

        Assert.Equal(7, uae.Regions.Count);
        Assert.Contains(uae.Regions, r => r.Name == "Abu Dhabi");
        Assert.Equal(36, india.Regions.Count); // 28 states + 8 union territories
        Assert.Contains(india.Regions, r => r.Name == "Tamil Nadu");
        Assert.True(india.PostalCodeRequired);
        Assert.False(uae.PostalCodeRequired);
    }

    // ------------------------------------------------------------------ service rules (need the Countries table)

    [Theory]
    [InlineData("600001", true)]
    [InlineData("60001", false)]   // 5 digits
    [InlineData("060001", false)]  // PIN codes do not start with 0
    [InlineData("6000O1", false)]  // letter O
    [InlineData(null, false)]      // required for India
    public async Task An_indian_postcode_must_be_six_digits(string? postcode, bool valid)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var india = await CountryAsync(db, "IN");
        var service = scope.ServiceProvider.GetRequiredService<IAddressService>();

        var request = Request(india, AddressTypes.Home, true, postcode);
        if (valid)
        {
            var built = await service.BuildAsync(new[] { request }, default);
            Assert.Equal(postcode, built.Single().PostalCode);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<RequestValidationException>(() => service.BuildAsync(new[] { request }, default));
            Assert.Contains("Addresses[0].PostalCode", ex.Errors.Keys);
        }
    }

    [Fact]
    public async Task A_uae_address_needs_no_postcode_but_needs_an_emirate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var service = scope.ServiceProvider.GetRequiredService<IAddressService>();

        var built = await service.BuildAsync(new[] { Request(uae, AddressTypes.Home, true, null) }, default);
        Assert.Null(built.Single().PostalCode);

        var noRegion = Request(uae, AddressTypes.Home, true, null) with { RegionId = null };
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => service.BuildAsync(new[] { noRegion }, default));
        Assert.Contains("Addresses[0].RegionId", ex.Errors.Keys);
    }

    [Fact]
    public async Task Saving_a_parent_replaces_addresses_by_type_and_can_move_the_primary()
    {
        int parentId;
        Country uae;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            uae = await CountryAsync(db, "AE");
            var service = scope.ServiceProvider.GetRequiredService<IParentService>();
            var created = await service.CreateAsync(ParentRequest(new List<AddressRequest>
            {
                Request(uae, AddressTypes.Home, true, null),
                Request(uae, AddressTypes.Permanent, false, null),
            }), default);
            parentId = created.Id;
        }

        // Home stays but stops being primary, Permanent is removed, Mailing is added as primary:
        // the old and new primary would clash in the unique index without the two-step save.
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IParentService>();
            var current = await service.GetByIdAsync(parentId, default);
            var homeId = current.Addresses.Single(a => a.AddressType == AddressTypes.Home).Id;
            var update = UpdateRequest(current, new List<AddressRequest>
            {
                Request(uae, AddressTypes.Home, false, null) with { Line1 = "Villa 9" },
                Request(uae, AddressTypes.Mailing, true, null) with { Line1 = "PO Box 123" },
            });

            var saved = await service.UpdateAsync(parentId, update, default);

            Assert.Equal(2, saved.Addresses.Count);
            var home = saved.Addresses.Single(a => a.AddressType == AddressTypes.Home);
            Assert.Equal(homeId, home.Id); // same type = same row, so its history carries on
            Assert.Equal("Villa 9", home.Line1);
            Assert.False(home.IsPrimary);
            Assert.True(saved.Addresses.Single(a => a.AddressType == AddressTypes.Mailing).IsPrimary);
        }
    }

    [Fact]
    public async Task Leaving_addresses_out_of_an_update_keeps_them()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await CountryAsync(db, "AE");
        var service = scope.ServiceProvider.GetRequiredService<IParentService>();
        var created = await service.CreateAsync(ParentRequest(new List<AddressRequest> { Request(uae, AddressTypes.Home, true, null) }), default);

        var saved = await service.UpdateAsync(created.Id, UpdateRequest(created, null), default);
        Assert.Single(saved.Addresses);
    }

    [Fact]
    public async Task Enrolment_copies_the_addresses_to_the_student_and_the_application_keeps_its_own()
    {
        int admissionId;
        ClassSection section;
        AdmissionGuardian guardian;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var india = await CountryAsync(db, "IN");
            section = await TestData.CreateSectionAsync(db);
            Admission admission;
            (admission, guardian) = await TestData.CreateAdmittedAdmissionAsync(db, section);
            admission.Addresses.Add(new Address
            {
                AddressType = AddressTypes.Home, IsPrimary = true, Line1 = "12 Anna Salai", City = "Chennai",
                CountryId = india.Id, RegionId = india.Regions.Single(r => r.Name == "Tamil Nadu").Id, PostalCode = "600002",
            });
            await db.SaveChangesAsync();
            admissionId = admission.Id;
        }

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAdmissionService>()
                .EnrollAsync(admissionId, TestData.EnrolRequest(section, guardian, rollNumber: "1"), default);

        using var verify = _factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var studentId = await vdb.Students.Where(s => s.AdmissionId == admissionId).Select(s => s.Id).SingleAsync();
        var studentAddress = await vdb.Addresses.SingleAsync(a => a.StudentId == studentId);
        var applicationAddress = await vdb.Addresses.SingleAsync(a => a.AdmissionId == admissionId);

        Assert.NotEqual(applicationAddress.Id, studentAddress.Id);
        Assert.Equal("12 Anna Salai", studentAddress.Line1);
        Assert.Equal("600002", studentAddress.PostalCode);
        Assert.True(studentAddress.IsPrimary);
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<Country> CountryAsync(ApplicationDbContext db, string code) =>
        await db.Countries.Include(c => c.Regions).SingleAsync(c => c.Code == code);

    private static async Task<Parent> NewParentAsync(ApplicationDbContext db)
    {
        var parent = new Parent { Name = "Parent " + TestData.Unique(), Mobile = "8" + Random.Shared.NextInt64(100_000_000, 999_999_999) };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        return parent;
    }

    private static Address NewAddress(Country country, string type, bool primary = false) => new()
    {
        AddressType = type,
        IsPrimary = primary,
        Line1 = "Villa 1",
        City = "Abu Dhabi",
        CountryId = country.Id,
        RegionId = country.Regions.First().Id,
    };

    private static AddressRequest Request(Country country, string type, bool primary, string? postcode) =>
        new(type, primary, "Line one", null, "City", country.Id, country.Regions.First().Id, postcode);

    private static CreateParentRequest ParentRequest(List<AddressRequest> addresses) => new(
        "Parent " + TestData.Unique(), null, "7" + Random.Shared.NextInt64(100_000_000, 999_999_999),
        null, null, null, null, null, null, null, false,
        true, true, true, true, true,
        null, null, null, null, false,
        addresses);

    private static UpdateParentRequest UpdateRequest(ParentResponse p, List<AddressRequest>? addresses) => new(
        p.Name, p.Email, p.Mobile, p.Status,
        null, null, null, null, null, null, null, false,
        true, true, true, true, true,
        null, null, null, null, false,
        addresses,
        p.RowVersion);

    /// <summary>The save fails, and the database names the rule that stopped it.</summary>
    private static async Task AssertViolatesAsync(ApplicationDbContext db, string constraintOrIndex)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains(constraintOrIndex, sql.Message);
    }
}
