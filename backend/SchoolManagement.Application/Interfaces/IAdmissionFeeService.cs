using SchoolManagement.Application.DTOs.AdmissionFees;

namespace SchoolManagement.Application.Interfaces;

public interface IAdmissionFeeService
{
    /// <summary>One row per class that exists, with its fee or null.</summary>
    Task<List<AdmissionFeeResponse>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Sets or replaces the fee of a class. 404 when no such class exists.</summary>
    Task<AdmissionFeeResponse> SetAsync(string className, SetAdmissionFeeRequest request, CancellationToken cancellationToken);

    /// <summary>Removes the fee of a class (it then has no suggested fee).</summary>
    Task ClearAsync(string className, CancellationToken cancellationToken);

    /// <summary>The fee for an application's class, or null Amount when none is set.</summary>
    Task<SuggestedAdmissionFeeResponse> GetSuggestedForAdmissionAsync(int admissionId, CancellationToken cancellationToken);
}
