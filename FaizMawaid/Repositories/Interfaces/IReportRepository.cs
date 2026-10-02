using FaizMawaid.Models.Dtos;

namespace FaizMawaid.Repositories.Interfaces
{
    public interface IReportRepository
    {
        /// <summary>How many thaalis of each size to prepare for a given date -- approved/active families minus anyone with an active cancellation that day.</summary>
        Task<DailyThaaliCountResponse> GetDailyThaaliCountAsync(DateOnly date);

        /// <summary>Which families have an active cancellation on each date in a range (inclusive) -- Sabil Number, address and thaali size for each, grouped by day, so Admin can tell whoever delivers/serves thaalis on any of those days.</summary>
        Task<CancelledThaaliRangeResponse> GetCancelledThaaliRangeAsync(DateOnly fromDate, DateOnly toDate);

        /// <summary>Month-wise, Area-wise count of thaalis actually distributed (served) -- every approved/active family minus anyone cancelled that day, summed across every serving day in [fromDate, toDate].</summary>
        Task<MonthlyAreaReportResponse> GetMonthlyThaaliDistributedByAreaAsync(DateOnly fromDate, DateOnly toDate);

        /// <summary>Month-wise, Area-wise count of thaali cancellations -- a family cancelled for N days in the range counts N times, summed across [fromDate, toDate].</summary>
        Task<MonthlyAreaReportResponse> GetMonthlyThaaliCancelledByAreaAsync(DateOnly fromDate, DateOnly toDate);
    }
}
