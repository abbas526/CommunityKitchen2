using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IAddressChangeRequestRepository
    {
        Task<AddressChangeRequest?> GetByIdAsync(ulong id);

        /// <summary>A family's own request history, newest first.</summary>
        Task<IEnumerable<AddressChangeRequest>> GetByFamilyIdAsync(ulong familyId);

        /// <summary>Admin view -- every family's requests, optionally filtered by status, newest first.</summary>
        Task<IEnumerable<AddressChangeRequest>> GetAllAsync(AddressChangeRequestStatus? status);

        /// <summary>How many requests are still waiting for an Admin decision -- for the admin dashboard tile/nav badge.</summary>
        Task<int> CountPendingAsync();

        Task<ulong> CreateAsync(CreateAddressChangeRequestRequest request);

        /// <summary>
        /// Approves a Pending request: applies NewAddress/NewAreaId to the Families row and
        /// marks the request Approved, in one transaction. Returns false if the request
        /// doesn't exist or isn't Pending (already reviewed).
        /// </summary>
        Task<bool> ApproveAsync(ulong id, ReviewAddressChangeRequestRequest request);

        /// <summary>Rejects a Pending request -- no change to Families. Returns false if the
        /// request doesn't exist or isn't Pending.</summary>
        Task<bool> RejectAsync(ulong id, ReviewAddressChangeRequestRequest request);

        /// <summary>Marks that the family has seen the one-time flash about this request's
        /// outcome. Idempotent -- only the first call for a given id has any effect.</summary>
        Task<bool> MarkNotifiedAsync(ulong id);
    }
}
