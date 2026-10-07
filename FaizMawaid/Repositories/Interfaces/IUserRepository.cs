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

        /// <summary>How many ACTIVE users hold this role -- used for the "N of 2 SuperAdmin seats" indicator.</summary>
        Task<int> CountActiveByRoleAsync(byte roleId);

        /// <summary>
        /// Creates a user as a SuperAdmin (request.RoleId is forced to RoleIds.SuperAdmin) -- but only
        /// if fewer than RoleIds.MaxActiveSuperAdmins ACTIVE SuperAdmins exist. The count and the insert
        /// happen in one transaction that first locks the SuperAdmin Roles row, so two simultaneous
        /// requests can't both slip past the limit. Returns SeatsFull (and creates nothing) otherwise.
        /// </summary>
        Task<UserCreateResult> CreateSuperAdminAsync(CreateUserRequest request);

        /// <summary>
        /// Moves an Admin or SuperAdmin between those two roles (newRoleId must be RoleIds.Admin or
        /// RoleIds.SuperAdmin). Promoting an ACTIVE account returns SeatsFull if no seat is free;
        /// demoting the last ACTIVE SuperAdmin returns WouldLeaveNoSuperAdmin. Same locking as above.
        /// Setting the role the user already has is a successful no-op.
        /// </summary>
        Task<UserChangeResult> SetRoleAsync(ulong id, byte newRoleId);

        /// <summary>
        /// Activate/deactivate with the SuperAdmin rules applied: re-activating a SuperAdmin needs a free
        /// seat (SeatsFull), and deactivating the last ACTIVE SuperAdmin is refused
        /// (WouldLeaveNoSuperAdmin). For any other role this is a plain update. Setting the state the
        /// user is already in is a successful no-op.
        /// </summary>
        Task<UserChangeResult> SetActiveGuardedAsync(ulong id, bool isActive);
    }
}
