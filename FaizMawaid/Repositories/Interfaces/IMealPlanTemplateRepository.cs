using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IMealPlanTemplateRepository
    {
        Task<IEnumerable<MealPlanTemplate>> GetAllAsync();
        Task<MealPlanTemplate?> GetByIdAsync(ushort id);
        Task<ushort> CreateAsync(CreateMealPlanTemplateRequest request);
        Task<bool> UpdateAsync(ushort id, UpdateMealPlanTemplateRequest request);
        Task<bool> DeleteAsync(ushort id);
    }
}
