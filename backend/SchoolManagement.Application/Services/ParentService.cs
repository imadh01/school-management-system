using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class ParentService : IParentService
{
    private readonly IParentRepository _parentRepository;

    public ParentService(IParentRepository parentRepository)
    {
        _parentRepository = parentRepository;
    }

    public async Task<List<ParentResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var parents = await _parentRepository.GetAllAsync(cancellationToken);
        return parents.Select(ToResponse).ToList();
    }

    public async Task<ParentResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var parent = await GetOrThrowAsync(id, cancellationToken);
        return ToResponse(parent);
    }

    public async Task<ParentResponse> CreateAsync(CreateParentRequest request, CancellationToken cancellationToken)
    {
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
            AddressLine = request.AddressLine,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
        };

        await _parentRepository.AddAsync(parent, cancellationToken);
        return ToResponse(parent);
    }

    public async Task<ParentResponse> UpdateAsync(int id, UpdateParentRequest request, CancellationToken cancellationToken)
    {
        var parent = await GetOrThrowAsync(id, cancellationToken);

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
        parent.AddressLine = request.AddressLine; parent.City = request.City;
        parent.State = request.State; parent.Pincode = request.Pincode;

        await _parentRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(parent);
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

    private static ParentResponse ToResponse(Parent p) => new(
        p.Id, p.Name, p.Email, p.Mobile, p.Status,
        p.Occupation, p.Nationality, p.CountryOfResidence, p.Timezone,
        p.PreferredLanguage, p.PreferredContactMethod, p.Whatsapp, p.EmergencyOnly,
        p.NotifyAttendance, p.NotifyExams, p.NotifyFees, p.NotifyNotices, p.NotifyDiscipline,
        p.Employer, p.JobTitle, p.WorkEmail, p.WorkPhone, p.BillingContact,
        p.AddressLine, p.City, p.State, p.Pincode);
}