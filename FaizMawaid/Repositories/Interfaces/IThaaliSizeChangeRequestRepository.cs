using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IThaaliSizeChangeRequestRepository
    {
        Task<ThaaliSizeChangeRequest?> GetByIdAsync(ulong id);

        /// <summary>A family's own request history, newest first.</summary>
        Task<IEnumerable<ThaaliSizeChangeRequest>> GetByFamilyIdAsync(ulong familyId);

        /// <summary>Admin view -- every family's requests, optionally filtered by status, newest first.</summary>
        Task<IEnumerable<ThaaliSizeChangeRequest>> GetAllAsync(ThaaliSizeChangeRequestStatus? status);

        /// <summary>How many requests are still waiting for an Admin decision -- for the admin dashboard tile/nav badge.</summary>
        Task<int> CountPendingAsync();

        Task<ulong> CreateAsync(CreateThaaliSizeChangeRequestRequest request);

        /// <summary>
        /// Approves a Pending request: closes the family's currently-open FamilySizeHistory
        /// row, opens a new one dated EffectiveFromDate, updates Families.ThaaliSizeId, and
        /// marks the request Approved -- all in one transaction. Returns false if the
        /// request doesn't exist or isn't Pending (already reviewed).
        /// </summary>
        Task<bool> ApproveAsync(ulong id, ReviewThaaliSizeChangeRequestRequest request);

        /// <summary>Rejects a Pending request -- no change to Families/FamilySizeHistory.
        /// Returns false if the request doesn't exist or isn't Pending.</summary>
        Task<bool> RejectAsync(ulong id, ReviewThaaliSizeChangeRequestRequest request);

        /// <summary>Marks that the family has seen the one-time flash about this request's
        /// outcome. Idempotent -- only the first call for a given id has any effect.</summary>
        Task<bool> MarkNotifiedAsync(ulong id);
    }
}
