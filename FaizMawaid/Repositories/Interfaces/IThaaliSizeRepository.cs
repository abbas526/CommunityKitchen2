using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IThaaliSizeRepository
    {
        Task<IEnumerable<ThaaliSize>> GetAllAsync();
        Task<ThaaliSize?> GetByIdAsync(byte id);
        Task<byte> CreateAsync(CreateThaaliSizeRequest request);
        Task<bool> UpdateAsync(byte id, UpdateThaaliSizeRequest request);
        /// <summary>True if any Family (current) or FamilySizeHistory (historical) row still references this size.</summary>
        Task<bool> IsInUseAsync(byte id);
        Task<bool> DeleteAsync(byte id);
    }
}
