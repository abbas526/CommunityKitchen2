using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IReportRepository
    {
        /// <summary>How many thaalis of each size to prepare for a given date -- approved/active families minus anyone with an active cancellation that day.</summary>
        Task<DailyThaaliCountResponse> GetDailyThaaliCountAsync(DateOnly date);

        /// <summary>Which families have an active cancellation on each date in a range (inclusive) -- Sabil Number, address and thaali size for each, grouped by day, so Admin can tell whoever delivers/serves thaalis on any of those days.</summary>
        Task<CancelledThaaliRangeResponse> GetCancelledThaaliRangeAsync(DateOnly fromDate, DateOnly toDate);
    }
}
