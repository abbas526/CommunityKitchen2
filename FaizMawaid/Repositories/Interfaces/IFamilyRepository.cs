using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IFamilyRepository
    {
        Task<Family?> GetByIdAsync(ulong id);

        /// <summary>Looks up a family by its FamilyHead's User id -- used by login to attach a FamilyId to the token response.</summary>
        Task<Family?> GetByFamilyHeadUserIdAsync(ulong userId);

        /// <summary>Primary families only -- sub-families (ParentFamilyId set) are excluded; fetch those via GetSubFamiliesAsync.</summary>
        Task<IEnumerable<Family>> GetAllAsync(RegistrationStatus? status = null);

        /// <summary>Creates the FamilyHead User row and the Family row together in one transaction.</summary>
        Task<RegisterFamilyResponse> RegisterAsync(RegisterFamilyRequest request);

        Task<bool> ApproveAsync(ulong familyId, ulong approvedByAdminUserId);
        Task<bool> RejectAsync(ulong familyId, ulong rejectedByAdminUserId);
        Task<bool> UpdateAsync(ulong familyId, UpdateFamilyRequest request);

        /// <summary>Closes the open FamilySizeHistory row, inserts a new one, and updates Families.ThaaliSizeId -- all in one transaction.</summary>
        Task<bool> ChangeThaaliSizeAsync(ulong familyId, ChangeThaaliSizeRequest request);

        Task<bool> SetActiveAsync(ulong familyId, bool isActive);

        /// <summary>Approved + active families -- the population any daily count/report is drawn from. Includes sub-families (each takes its own Thaali).</summary>
        Task<IEnumerable<Family>> GetActiveApprovedAsync();

        /// <summary>All sub-family rows linked to the given primary family.</summary>
        Task<IEnumerable<Family>> GetSubFamiliesAsync(ulong parentFamilyId);

        /// <summary>Admin-only: links a new sub-family (no login of its own) to an existing primary family, together with its opening FamilySizeHistory row, in one transaction.</summary>
        Task<ulong> CreateSubFamilyAsync(ulong parentFamilyId, CreateSubFamilyRequest request);

        /// <summary>
        /// Bulk-import path: creates the FamilyHead User row, the Family row, and the opening
        /// FamilySizeHistory row together in one transaction -- like RegisterAsync, but the
        /// Family is created already Approved and active (the Admin is vouching for every row
        /// in the sheet) instead of Pending, and MustChangePassword is forced true (the Admin,
        /// not the member, chose the password).
        /// </summary>
        Task<RegisterFamilyResponse> ImportApprovedFamilyAsync(RegisterFamilyRequest request, ulong approvedByAdminUserId);
    }
}
