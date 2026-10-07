using System.Text.Json;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // SuperAdmin-only (2026-10-07): a regular Admin can no longer read or change App Settings.
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public class AppSettingsController : ControllerBase
    {
        private readonly IAppSettingsRepository _appSettingsRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public AppSettingsController(IAppSettingsRepository appSettingsRepository, IAuditLogRepository auditLogRepository)
        {
            _appSettingsRepository = appSettingsRepository;
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet]
        public async Task<ActionResult<AppSetting>> Get()
        {
            return Ok(await _appSettingsRepository.GetAsync());
        }

        [HttpPut]
        public async Task<IActionResult> Update(UpdateAppSettingsRequest request)
        {
            if (request.MealVisibilityDays == 0)
            {
                return BadRequest("MealVisibilityDays must be at least 1.");
            }

            var updated = await _appSettingsRepository.UpdateAsync(request);
            if (!updated)
            {
                return NotFound();
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.UpdatedByUserId,
                Action = "AppSettingsUpdated",
                EntityType = "AppSettings",
                EntityId = 1,
                MetadataJson = JsonSerializer.Serialize(new { request.MealVisibilityDays }),
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }
    }
}
