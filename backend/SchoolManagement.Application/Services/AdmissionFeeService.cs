using SchoolManagement.Application.DTOs.AdmissionFees;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class AdmissionFeeService : IAdmissionFeeService
{
    private readonly IAdmissionFeeRepository _fees;
    private readonly IAdmissionRepository _admissions;

    public AdmissionFeeService(IAdmissionFeeRepository fees, IAdmissionRepository admissions)
    {
        _fees = fees;
        _admissions = admissions;
    }

    public async Task<List<AdmissionFeeResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var names = await _fees.GetClassNamesAsync(cancellationToken);
        var fees = (await _fees.GetAllAsync(cancellationToken)).ToDictionary(f => f.ClassName);
        return names
            .Select(n => new AdmissionFeeResponse(n, fees.TryGetValue(n, out var fee) ? fee.Amount : null))
            .ToList();
    }

    public async Task<AdmissionFeeResponse> SetAsync(
        string className, SetAdmissionFeeRequest request, CancellationToken cancellationToken)
    {
        className = className.Trim();
        await EnsureClassExistsAsync(className, cancellationToken);

        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var fee = await _fees.GetByClassNameAsync(className, cancellationToken);
        if (fee is null)
            await _fees.AddAsync(new AdmissionFee { ClassName = className, Amount = amount }, cancellationToken);
        else
        {
            fee.Amount = amount;
            await _fees.SaveChangesAsync(cancellationToken);
        }
        return new AdmissionFeeResponse(className, amount);
    }

    public async Task ClearAsync(string className, CancellationToken cancellationToken)
    {
        className = className.Trim();
        await EnsureClassExistsAsync(className, cancellationToken);
        var fee = await _fees.GetByClassNameAsync(className, cancellationToken);
        if (fee is not null) await _fees.RemoveAsync(fee, cancellationToken);
    }

    public async Task<SuggestedAdmissionFeeResponse> GetSuggestedForAdmissionAsync(
        int admissionId, CancellationToken cancellationToken)
    {
        var admission = await _admissions.GetByIdAsync(admissionId, cancellationToken)
            ?? throw new NotFoundException($"Application {admissionId} does not exist.");
        var className = admission.AppliedForClassSection.Name;
        var fee = await _fees.GetByClassNameAsync(className, cancellationToken);
        return new SuggestedAdmissionFeeResponse(className, fee?.Amount);
    }

    private async Task EnsureClassExistsAsync(string className, CancellationToken cancellationToken)
    {
        var names = await _fees.GetClassNamesAsync(cancellationToken);
        if (!names.Contains(className))
            throw new NotFoundException($"There is no class named '{className}'.");
    }
}
