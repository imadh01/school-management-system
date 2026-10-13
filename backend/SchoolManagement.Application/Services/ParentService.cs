using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class ParentService : IParentService
{
    private readonly IParentRepository _parentRepository;
    private readonly IConcurrencyGuard _concurrency;
    private readonly IAddressService _addressService;

    public ParentService(IParentRepository parentRepository, IConcurrencyGuard concurrency, IAddressService addressService)
    {
        _parentRepository = parentRepository;
        _concurrency = concurrency;
        _addressService = addressService;
    }

    public async Task<List<ParentResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var parents = await _parentRepository.GetAllAsync(DataScope.Unrestricted, cancellationToken);
        return parents.Select(ToResponse).ToList();
    }

    public async Task<List<ParentContactResponse>> GetAllScopedAsync(DataScope scope, CancellationToken cancellationToken)
    {
        var parents = await _parentRepository.GetAllAsync(scope, cancellationToken);
        return parents.Select(ToContact).ToList();
    }

    public async Task<ParentResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var parent = await GetOrThrowAsync(id, cancellationToken);
        return ToResponse(parent);
    }

    public async Task<ParentContactResponse> GetByIdScopedAsync(int id, DataScope scope, CancellationToken cancellationToken)
    {
        // Same message as GetOrThrowAsync: a missing and an out-of-scope parent are indistinguishable.
        var parent = await _parentRepository.GetVisibleByIdAsync(id, scope, cancellationToken)
            ?? throw new NotFoundException($"Parent {id} does not exist.");
        return ToContact(parent);
    }

    public async Task<ParentResponse> CreateAsync(CreateParentRequest request, CancellationToken cancellationToken)
    {
        var addresses = await _addressService.BuildAsync(request.Addresses ?? new(), cancellationToken);
        var parent = new Parent
        {
            Name = request.Name,
            Email = request.Email,
            Mobile = request.Mobile,
            Status = "Active",
            Occupation = request.Occupation,
            Nationality = request.Nationality,
            CountryOfResidence = request.CountryOfResidence,
            Timezone = request.Timezone,
            PreferredLanguage = request.PreferredLanguage,
            PreferredContactMethod = request.PreferredContactMethod,
            Whatsapp = request.Whatsapp,
            EmergencyOnly = request.EmergencyOnly,
            NotifyAttendance = request.NotifyAttendance,
            NotifyExams = request.NotifyExams,
            NotifyFees = request.NotifyFees,
            NotifyNotices = request.NotifyNotices,
            NotifyDiscipline = request.NotifyDiscipline,
            Employer = request.Employer,
            JobTitle = request.JobTitle,
            WorkEmail = request.WorkEmail,
            WorkPhone = request.WorkPhone,
            BillingContact = request.BillingContact,
            Addresses = addresses,
        };

        await _parentRepository.AddAsync(parent, cancellationToken);
        return ToResponse(parent);
    }

    public async Task<ParentResponse> UpdateAsync(int id, UpdateParentRequest request, CancellationToken cancellationToken)
    {
        var parent = await GetOrThrowAsync(id, cancellationToken);
        _concurrency.Expect(parent, request.RowVersion); // 409 if someone saved this parent after it was loaded
        var addresses = request.Addresses is null
            ? null
            : await _addressService.BuildAsync(request.Addresses, cancellationToken);

        parent.Name = request.Name; parent.Email = request.Email; parent.Mobile = request.Mobile;
        parent.Status = request.Status;
        parent.Occupation = request.Occupation; parent.Nationality = request.Nationality;
        parent.CountryOfResidence = request.CountryOfResidence; parent.Timezone = request.Timezone;
        parent.PreferredLanguage = request.PreferredLanguage; parent.PreferredContactMethod = request.PreferredContactMethod;
        parent.Whatsapp = request.Whatsapp; parent.EmergencyOnly = request.EmergencyOnly;
        parent.NotifyAttendance = request.NotifyAttendance; parent.NotifyExams = request.NotifyExams;
        parent.NotifyFees = request.NotifyFees; parent.NotifyNotices = request.NotifyNotices;
        parent.NotifyDiscipline = request.NotifyDiscipline;
        parent.Employer = request.Employer; parent.JobTitle = request.JobTitle;
        parent.WorkEmail = request.WorkEmail; parent.WorkPhone = request.WorkPhone;
        parent.BillingContact = request.BillingContact;

        if (addresses is null)
        {
            await _parentRepository.SaveChangesAsync(cancellationToken);
            return ToResponse(parent);
        }

        // Replacing addresses may save twice: one transaction, so a failure leaves nothing half-done.
        return await _parentRepository.ExecuteInTransactionAsync(async () =>
        {
            await _addressService.ReplaceAsync(parent.Addresses, addresses, cancellationToken);
            await _parentRepository.SaveChangesAsync(cancellationToken);
            return ToResponse(parent);
        }, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var parent = await GetOrThrowAsync(id, cancellationToken);
        parent.IsDeleted = true;
        parent.DeletedAt = DateTime.UtcNow;
        await _parentRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Parent> GetOrThrowAsync(int id, CancellationToken cancellationToken) =>
        await _parentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Parent {id} does not exist.");

    private static ParentContactResponse ToContact(Parent p) => new(p.Id, p.Name, p.Mobile);

    private static ParentResponse ToResponse(Parent p) => new(
        p.Id, p.Name, p.Email, p.Mobile, p.Status,
        p.Occupation, p.Nationality, p.CountryOfResidence, p.Timezone,
        p.PreferredLanguage, p.PreferredContactMethod, p.Whatsapp, p.EmergencyOnly,
        p.NotifyAttendance, p.NotifyExams, p.NotifyFees, p.NotifyNotices, p.NotifyDiscipline,
        p.Employer, p.JobTitle, p.WorkEmail, p.WorkPhone, p.BillingContact,
        AddressMapping.ToResponses(p.Addresses),
        AddressMapping.LegacyText(p.Addresses, p.AddressLine, p.City, p.State, p.Pincode),
        Convert.ToBase64String(p.RowVersion));
}
