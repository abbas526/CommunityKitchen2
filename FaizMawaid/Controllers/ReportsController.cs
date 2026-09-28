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
    }
}
