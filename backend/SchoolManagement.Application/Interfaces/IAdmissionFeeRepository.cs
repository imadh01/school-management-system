using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IAdmissionFeeRepository
{
    /// <summary>Every class name that exists (sections not deleted), in teaching order: LKG, UKG, Class 1 ...</summary>
    Task<List<string>> GetClassNamesAsync(CancellationToken cancellationToken);

    Task<List<AdmissionFee>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Tracked, so it can be changed and saved.</summary>
    Task<AdmissionFee?> GetByClassNameAsync(string className, CancellationToken cancellationToken);

    Task AddAsync(AdmissionFee fee, CancellationToken cancellationToken);
    Task RemoveAsync(AdmissionFee fee, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
