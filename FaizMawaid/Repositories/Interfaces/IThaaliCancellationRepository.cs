using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IThaaliCancellationRepository
    {
        Task<ThaaliCancellation?> GetByIdAsync(ulong id);
        Task<IEnumerable<ThaaliCancellation>> GetByFamilyIdAsync(ulong familyId);
        Task<IEnumerable<ThaaliCancellation>> GetByDateRangeAsync(DateOnly from, DateOnly to);
        Task<bool> HasActiveCancellationOnDateAsync(ulong familyId, DateOnly date);
        Task<ulong> CreateAsync(CreateThaaliCancellationRequest request);
        Task<bool> ReinstateAsync(ulong id, ulong reinstatedByUserId);

        /// <summary>Family ids with an Active cancellation covering the given date -- used by the daily report.</summary>
        Task<IEnumerable<ulong>> GetCancelledFamilyIdsOnDateAsync(DateOnly date);
    }
}
