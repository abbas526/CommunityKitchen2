using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    public class ReportsController : ControllerBase
    {
        /// <summary>Longest date range a monthly report will compute in one request -- generous for
        /// a report that's aggregated to month level, but still a sanity cap against an open-ended query.</summary>
        private const int MaxMonthlyReportDays = 1096; // ~3 years

        private readonly IReportRepository _reportRepository;

        public ReportsController(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository;
        }

        /// <summary>How many thaalis of each size to prepare for a given date -- the core "reduce food wastage" report.</summary>
        [HttpGet("daily-thaali-count")]
        public async Task<ActionResult<DailyThaaliCountResponse>> GetDailyThaaliCount([FromQuery] DateOnly date)
        {
            return Ok(await _reportRepository.GetDailyThaaliCountAsync(date));
        }

        /// <summary>Which families have cancelled their thaali on each date in a range (inclusive) -- Sabil Number, address and size for each, grouped by day -- so Admin can tell whoever delivers/serves thaalis on any day in that window, or the day before.</summary>
        [HttpGet("cancelled-thaalis")]
        public async Task<ActionResult<CancelledThaaliRangeResponse>> GetCancelledThaalis([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            if (to < from)
            {
                return BadRequest("The end date must be on or after the start date.");
            }
            if (to.DayNumber - from.DayNumber > 92)
            {
                return BadRequest("Please choose a range of 92 days or less.");
            }
            return Ok(await _reportRepository.GetCancelledThaaliRangeAsync(from, to));
        }

        /// <summary>Month-wise, Area-wise count of thaalis actually distributed. Defaults to 1 January of the current year through today when no dates are supplied.</summary>
        [HttpGet("monthly-thaali-distributed")]
        public async Task<ActionResult<MonthlyAreaReportResponse>> GetMonthlyThaaliDistributed([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            var (fromDate, toDate, error) = ResolveMonthlyReportRange(from, to);
            if (error is not null)
            {
                return BadRequest(error);
            }
            return Ok(await _reportRepository.GetMonthlyThaaliDistributedByAreaAsync(fromDate, toDate));
        }

        /// <summary>Month-wise, Area-wise count of thaali cancellations. Defaults to 1 January of the current year through today when no dates are supplied.</summary>
        [HttpGet("monthly-thaali-cancelled")]
        public async Task<ActionResult<MonthlyAreaReportResponse>> GetMonthlyThaaliCancelled([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            var (fromDate, toDate, error) = ResolveMonthlyReportRange(from, to);
            if (error is not null)
            {
                return BadRequest(error);
            }
            return Ok(await _reportRepository.GetMonthlyThaaliCancelledByAreaAsync(fromDate, toDate));
        }

        /// <summary>Shared default/validation logic for both monthly Area-wise reports: defaults to
        /// 1 January of the current (server UTC) year through today, and caps the range so an Admin
        /// can't request an unbounded aggregation.</summary>
        private static (DateOnly From, DateOnly To, string? Error) ResolveMonthlyReportRange(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var fromDate = from ?? new DateOnly(today.Year, 1, 1);
            var toDate = to ?? today;

            if (toDate < fromDate)
            {
                return (fromDate, toDate, "The end date must be on or after the start date.");
            }
            if (toDate.DayNumber - fromDate.DayNumber > MaxMonthlyReportDays)
            {
                return (fromDate, toDate, "Please choose a range of about 3 years or less.");
            }
            return (fromDate, toDate, null);
        }
    }
}
