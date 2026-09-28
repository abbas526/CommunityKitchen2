using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IMealPlanRepository
    {
        Task<MealPlan?> GetByIdAsync(ulong id);
        Task<MealPlan?> GetByDateAsync(DateOnly date);
        Task<IEnumerable<MealPlan>> GetRangeAsync(DateOnly from, DateOnly to);
        Task<ulong> CreateAsync(CreateMealPlanRequest request);
        Task<bool> UpdateAsync(ulong id, UpdateMealPlanRequest request);
        Task<bool> DeleteAsync(ulong id);
    }
}
