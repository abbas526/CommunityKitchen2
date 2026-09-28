using FaizMawaid.Models;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IFamilySizeHistoryRepository
    {
        Task<IEnumerable<FamilySizeHistory>> GetByFamilyIdAsync(ulong familyId);

        /// <summary>What size did this family have on a given date -- the historical-reporting use case.</summary>
        Task<FamilySizeHistory?> GetSizeOnDateAsync(ulong familyId, DateOnly date);
    }
}
