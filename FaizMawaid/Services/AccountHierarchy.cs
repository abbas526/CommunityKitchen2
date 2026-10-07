using System.Security.Claims;
using FaizMawaid.Models;

namespace FaizMawaid.Services
{
    /// <summary>
    /// Who may manage whose account (the SuperAdmin hierarchy, 2026-10-07):
    /// a SuperAdmin may manage anyone; a regular Admin may only manage Family Heads -- never
    /// another Admin or a SuperAdmin. The caller's identity always comes from the validated JWT,
    /// never from anything the client sends in a request body.
    /// Shared by UsersController and AuthController (admin-reset-password) so the rule lives in one place.
    /// </summary>
    public static class AccountHierarchy
    {
        /// <summary>Reads the caller's user id and SuperAdmin-ness from the JWT. False when there's no valid id claim.</summary>
        public static bool TryGetCaller(ClaimsPrincipal? user, out ulong callerId, out bool callerIsSuperAdmin)
        {
            callerId = 0;
            callerIsSuperAdmin = false;
            var idClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ulong.TryParse(idClaim, out callerId))
            {
                return false;
            }
            callerIsSuperAdmin = user!.IsInRole(RoleNames.SuperAdmin);
            return true;
        }

        /// <summary>True when a caller (SuperAdmin or regular Admin) is allowed to act on an account holding <paramref name="targetRoleId"/>.</summary>
        public static bool CanManage(bool callerIsSuperAdmin, byte targetRoleId)
        {
            return callerIsSuperAdmin || targetRoleId == RoleIds.FamilyHead;
        }

        public const string OnlySuperAdminMessage = "Only a SuperAdmin can manage Admin or SuperAdmin accounts.";
        public const string SeatsFullMessage = "There can be at most 2 active SuperAdmins. Deactivate or demote one first.";
        public const string LastSuperAdminMessage = "There must always be at least one active SuperAdmin. Make another account a SuperAdmin first.";
    }
}
