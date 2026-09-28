using FaizMawaid.Models;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogRepository _auditLogRepository;

        public AuditLogsController(IAuditLogRepository auditLogRepository)
        {
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuditLog>>> Get(
            [FromQuery] string? entityType,
            [FromQuery] ulong? entityId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to)
        {
            return Ok(await _auditLogRepository.GetAsync(entityType, entityId, from, to));
        }
    }
}
