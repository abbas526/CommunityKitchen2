using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface INonServingDayRepository
    {
        Task<IEnumerable<NonServingDay>> GetAllAsync(int? year = null);
        Task<bool> IsNonServingDayAsync(DateOnly date);
        Task<uint> CreateAsync(CreateNonServingDayRequest request);
        Task<bool> DeleteAsync(uint id);
    }
}
