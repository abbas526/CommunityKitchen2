using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IMealPlanRepository
    {
        Task<MealPlan?> GetByIdAsync(ulong id);
        Task<MealPlan?> GetByDateAsync(DateOnly date);
        Task<IEnumerable<MealPlan>> GetRangeAsync(DateOnly from, DateOnly to);
        /// <summary>Only the Special Days (every family receives the meal) in [from, to] -- used by reports and by the Family Head views of a family that only receives meals on Special Days.</summary>
        Task<IEnumerable<MealPlan>> GetSpecialDaysAsync(DateOnly from, DateOnly to);
        Task<ulong> CreateAsync(CreateMealPlanRequest request);
        Task<bool> UpdateAsync(ulong id, UpdateMealPlanRequest request);
        Task<bool> DeleteAsync(ulong id);
    }
}
