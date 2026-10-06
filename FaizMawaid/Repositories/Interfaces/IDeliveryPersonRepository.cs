using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IDeliveryPersonRepository
    {
        /// <summary>Every Delivery Person, active or not -- for the Admin management page.</summary>
        Task<IEnumerable<DeliveryPerson>> GetAllAsync();
        Task<DeliveryPerson?> GetByIdAsync(ulong id);
        /// <summary>Active Delivery Persons who serve one Area (a person may serve several Areas) -- what a family's dashboard shows.</summary>
        Task<IEnumerable<DeliveryPerson>> GetActiveByAreaIdAsync(byte areaId);
        Task<ulong> CreateAsync(CreateDeliveryPersonRequest request);
        Task<bool> UpdateAsync(ulong id, UpdateDeliveryPersonRequest request);
        Task<bool> DeleteAsync(ulong id);
    }
}
