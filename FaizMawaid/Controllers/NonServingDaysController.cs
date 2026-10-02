using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NonServingDaysController : ControllerBase
    {
        private readonly INonServingDayRepository _nonServingDayRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public NonServingDaysController(INonServingDayRepository nonServingDayRepository, IAuditLogRepository auditLogRepository)
        {
            _nonServingDayRepository = nonServingDayRepository;
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NonServingDay>>> GetAll([FromQuery] int? year)
        {
            return Ok(await _nonServingDayRepository.GetAllAsync(year));
        }

        /// <summary>Sundays, and the entire month of Ramadan (Hijri), don't need a row here --
        /// both are fixed rules applied in code. This is only for the extra exception dates.</summary>
        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Create(CreateNonServingDayRequest request)
        {
            if (request.TheDate.DayOfWeek == DayOfWeek.Sunday)
            {
                return BadRequest("Sundays are already a non-serving day by default -- no need to add them here.");
            }

            if (MisriCalendar.IsRamadan(request.TheDate))
            {
                return BadRequest("The entire month of Ramadan is already a non-serving period by default -- no need to add it here.");
            }

            uint id;
            try
            {
                id = await _nonServingDayRepository.CreateAsync(request);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict($"{request.TheDate:yyyy-MM-dd} is already marked as a non-serving day.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "NonServingDayCreated",
                EntityType = "NonServingDay",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return CreatedAtAction(nameof(GetAll), new { year = request.TheDate.Year }, new { Id = id, request.TheDate, request.Reason });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(uint id)
        {
            var deleted = await _nonServingDayRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
