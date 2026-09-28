using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ThaaliCancellationsController : ControllerBase
    {
        private readonly IThaaliCancellationRepository _cancellationRepository;
        private readonly IFamilyRepository _familyRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public ThaaliCancellationsController(
            IThaaliCancellationRepository cancellationRepository,
            IFamilyRepository familyRepository,
            IAuditLogRepository auditLogRepository)
        {
            _cancellationRepository = cancellationRepository;
            _familyRepository = familyRepository;
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ThaaliCancellation>> GetById(ulong id)
        {
            var cancellation = await _cancellationRepository.GetByIdAsync(id);
            return cancellation is null ? NotFound() : Ok(cancellation);
        }

        [HttpGet("family/{familyId}")]
        public async Task<ActionResult<IEnumerable<ThaaliCancellation>>> GetByFamily(ulong familyId)
        {
            return Ok(await _cancellationRepository.GetByFamilyIdAsync(familyId));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ThaaliCancellation>>> GetByDateRange([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            if (to < from)
            {
                return BadRequest("'to' must not be before 'from'.");
            }
            return Ok(await _cancellationRepository.GetByDateRangeAsync(from, to));
        }

        /// <summary>Family Head cancels their own thaali (or an Admin enters it on their behalf via CreatedByUserId).</summary>
        [HttpPost]
        public async Task<ActionResult<ThaaliCancellation>> Create(CreateThaaliCancellationRequest request)
        {
            if (request.EndDate < request.StartDate)
            {
                return BadRequest("EndDate must not be before StartDate.");
            }

            if (request.StartDate < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return BadRequest("StartDate cannot be in the past.");
            }

            var family = await _familyRepository.GetByIdAsync(request.FamilyId);
            if (family is null || family.RegistrationStatus != RegistrationStatus.Approved || !family.IsActive)
            {
                return BadRequest("Family must exist and be an approved, active family to cancel a thaali.");
            }

            var id = await _cancellationRepository.CreateAsync(request);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "ThaaliCancellationCreated",
                EntityType = "ThaaliCancellation",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            var created = await _cancellationRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        /// <summary>The family decides they do want the thaali after all -- undoes an Active cancellation.</summary>
        [HttpPut("{id}/reinstate")]
        public async Task<IActionResult> Reinstate(ulong id, ReinstateThaaliCancellationRequest request)
        {
            var reinstated = await _cancellationRepository.ReinstateAsync(id, request.ReinstatedByUserId);
            if (!reinstated)
            {
                return Conflict("Cancellation was not found, or is not Active.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ReinstatedByUserId,
                Action = "ThaaliCancellationReinstated",
                EntityType = "ThaaliCancellation",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }
    }
}
