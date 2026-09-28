using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IFeedbackRepository
    {
        Task<Feedback?> GetByIdAsync(ulong id);
        /// <summary>A family's own feedback history, newest first.</summary>
        Task<IEnumerable<Feedback>> GetByFamilyIdAsync(ulong familyId);
        /// <summary>Admin view -- every family's feedback, optionally filtered by status, newest first.</summary>
        Task<IEnumerable<Feedback>> GetAllAsync(FeedbackStatus? status);
        /// <summary>How many feedback items are still waiting for an Admin response -- for the admin dashboard tile.</summary>
        Task<int> CountOpenAsync();
        Task<ulong> CreateAsync(CreateFeedbackRequest request);
        /// <summary>Sets the response text and flips Status to Responded. Returns false if no row with that id exists.</summary>
        Task<bool> RespondAsync(ulong id, RespondFeedbackRequest request);
    }
}
