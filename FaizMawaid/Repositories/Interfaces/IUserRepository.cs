using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(ulong id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetBySabilNumberAsync(string sabilNumber);
        Task<IEnumerable<User>> GetAllAsync(byte? roleId = null);
        Task<ulong> CreateAsync(CreateUserRequest request);
        Task<bool> UpdateAsync(ulong id, UpdateUserRequest request);
        Task<bool> SetActiveAsync(ulong id, bool isActive);
        Task<bool> SetPasswordAsync(ulong id, string passwordHash, bool mustChangePassword);
    }
}
