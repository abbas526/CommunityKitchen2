using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IAreaRepository
    {
        Task<IEnumerable<Area>> GetAllAsync();
        Task<Area?> GetByIdAsync(byte id);
        Task<byte> CreateAsync(CreateAreaRequest request);
        Task<bool> UpdateAsync(byte id, UpdateAreaRequest request);
        /// <summary>True if any Family or DeliveryPerson row still references this Area.</summary>
        Task<bool> IsInUseAsync(byte id);
        Task<bool> DeleteAsync(byte id);
    }
}
