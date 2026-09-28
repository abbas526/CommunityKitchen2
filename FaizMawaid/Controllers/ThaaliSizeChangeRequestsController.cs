using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    /// <summary>
    /// A Family Head's request to change their Thaali size, reviewed by an Admin.
    /// Class-level attribute is a bare [Authorize] (any signed-in user) -- GetAll/
    /// GetPendingCount/Approve/Reject each carry their own explicit
    /// [Authorize(Roles = Admin)] so they stay Admin-only, following the same safe
    /// pattern as AddressChangeRequestsController/FeedbackController/
    /// DeliveryPersonsController (never rely on a bare method-level [Authorize] to
    /// loosen a class-level Roles=Admin -- it doesn't).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ThaaliSizeChangeRequestsController : ControllerBase
    {
        private readonly IThaaliSizeChangeRequestRepository _thaaliSizeChangeRequestRepository;
        private readonly IFamilyRepository _familyRepository;
        private readonly IThaaliSizeRepository _thaaliSizeRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public ThaaliSizeChangeRequestsController(
            IThaaliSizeChangeRequestRepository thaaliSizeChangeRequestRepository,
            IFamilyRepository familyRepository,
            IThaaliSizeRepository thaaliSizeRepository,
            IAuditLogRepository auditLogRepository)
        {
            _thaaliSizeChangeRequestRepository = thaaliSizeChangeRequestRepository;
            _familyRepository = familyRepository;
            _thaaliSizeRepository = thaaliSizeRepository;
            _auditLogRepository = auditLogRepository;
        }

        /// <summary>Admin view -- every family's requests, optionally filtered by status (e.g. ?status=Pending).</summary>
        [HttpGet]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<IEnumerable<ThaaliSizeChangeRequest>>> GetAll([FromQuery] ThaaliSizeChangeRequestStatus? status)
        {
            return Ok(await _thaaliSizeChangeRequestRepository.GetAllAsync(status));
        }

        /// <summary>Admin dashboard tile/nav badge -- how many requests are still waiting for a decision.</summary>
        [HttpGet("pending-count")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<int>> GetPendingCount()
        {
            return Ok(await _thaaliSizeChangeRequestRepository.CountPendingAsync());
        }

        /// <summary>Family view -- their own request history (any signed-in user).</summary>
        [HttpGet("family/{familyId}")]
        public async Task<ActionResult<IEnumerable<ThaaliSizeChangeRequest>>> GetByFamily(ulong familyId)
        {
            return Ok(await _thaaliSizeChangeRequestRepository.GetByFamilyIdAsync(familyId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ThaaliSizeChangeRequest>> GetById(ulong id)
        {
            var item = await _thaaliSizeChangeRequestRepository.GetByIdAsync(id);
            return item is null ? NotFound() : Ok(item);
        }

        /// <summary>A Family Head requests a Thaali size change, effective on a given date.</summary>
        [HttpPost]
        public async Task<ActionResult<ThaaliSizeChangeRequest>> Create(CreateThaaliSizeChangeRequestRequest request)
        {
            var size = await _thaaliSizeRepository.GetByIdAsync(request.NewThaaliSizeId);
            if (size is null)
            {
                return BadRequest($"ThaaliSizeId {request.NewThaaliSizeId} does not exist.");
            }

            var family = await _familyRepository.GetByIdAsync(request.FamilyId);
            if (family is null)
            {
                return BadRequest($"FamilyId {request.FamilyId} does not exist.");
            }

            var id = await _thaaliSizeChangeRequestRepository.CreateAsync(request);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "ThaaliSizeChangeRequested",
                EntityType = "ThaaliSizeChangeRequest",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            var created = await _thaaliSizeChangeRequestRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        /// <summary>Admin approves a Pending request -- applies the size change (and its
        /// history record) immediately and marks the request Approved.</summary>
        [HttpPut("{id}/approve")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Approve(ulong id, ReviewThaaliSizeChangeRequestRequest request)
        {
            var existing = await _thaaliSizeChangeRequestRepository.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }
            if (existing.Status != ThaaliSizeChangeRequestStatus.Pending)
            {
                return Conflict($"This request has already been {existing.Status}.");
            }

            var approved = await _thaaliSizeChangeRequestRepository.ApproveAsync(id, request);
            if (!approved)
            {
                return Conflict("This request has already been reviewed.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ReviewedByAdminUserId,
                Action = "ThaaliSizeChangeApproved",
                EntityType = "ThaaliSizeChangeRequest",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        /// <summary>Admin rejects a Pending request -- no change to the family's record.</summary>
        [HttpPut("{id}/reject")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Reject(ulong id, ReviewThaaliSizeChangeRequestRequest request)
        {
            var existing = await _thaaliSizeChangeRequestRepository.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }
            if (existing.Status != ThaaliSizeChangeRequestStatus.Pending)
            {
                return Conflict($"This request has already been {existing.Status}.");
            }

            var rejected = await _thaaliSizeChangeRequestRepository.RejectAsync(id, request);
            if (!rejected)
            {
                return Conflict("This request has already been reviewed.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ReviewedByAdminUserId,
                Action = "ThaaliSizeChangeRejected",
                EntityType = "ThaaliSizeChangeRequest",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        /// <summary>Family marks that they've seen the one-time flash about this request's outcome.</summary>
        [HttpPost("{id}/mark-notified")]
        public async Task<IActionResult> MarkNotified(ulong id)
        {
            await _thaaliSizeChangeRequestRepository.MarkNotifiedAsync(id);
            return NoContent();
        }
    }
}
