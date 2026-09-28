using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    /// <summary>
    /// A Family Head's request to change their Address/Area, reviewed by an Admin.
    /// Class-level attribute is a bare [Authorize] (any signed-in user) -- GetAll/
    /// GetPendingCount/Approve/Reject each carry their own explicit
    /// [Authorize(Roles = Admin)] so they stay Admin-only, following the same safe
    /// pattern as FeedbackController/DeliveryPersonsController (never rely on a bare
    /// method-level [Authorize] to loosen a class-level Roles=Admin -- it doesn't).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AddressChangeRequestsController : ControllerBase
    {
        private readonly IAddressChangeRequestRepository _addressChangeRequestRepository;
        private readonly IFamilyRepository _familyRepository;
        private readonly IAreaRepository _areaRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public AddressChangeRequestsController(
            IAddressChangeRequestRepository addressChangeRequestRepository,
            IFamilyRepository familyRepository,
            IAreaRepository areaRepository,
            IAuditLogRepository auditLogRepository)
        {
            _addressChangeRequestRepository = addressChangeRequestRepository;
            _familyRepository = familyRepository;
            _areaRepository = areaRepository;
            _auditLogRepository = auditLogRepository;
        }

        /// <summary>Admin view -- every family's requests, optionally filtered by status (e.g. ?status=Pending).</summary>
        [HttpGet]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<IEnumerable<AddressChangeRequest>>> GetAll([FromQuery] AddressChangeRequestStatus? status)
        {
            return Ok(await _addressChangeRequestRepository.GetAllAsync(status));
        }

        /// <summary>Admin dashboard tile/nav badge -- how many requests are still waiting for a decision.</summary>
        [HttpGet("pending-count")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<int>> GetPendingCount()
        {
            return Ok(await _addressChangeRequestRepository.CountPendingAsync());
        }

        /// <summary>Family view -- their own request history (any signed-in user).</summary>
        [HttpGet("family/{familyId}")]
        public async Task<ActionResult<IEnumerable<AddressChangeRequest>>> GetByFamily(ulong familyId)
        {
            return Ok(await _addressChangeRequestRepository.GetByFamilyIdAsync(familyId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AddressChangeRequest>> GetById(ulong id)
        {
            var item = await _addressChangeRequestRepository.GetByIdAsync(id);
            return item is null ? NotFound() : Ok(item);
        }

        /// <summary>A Family Head requests an Address/Area change, effective on a given date.</summary>
        [HttpPost]
        public async Task<ActionResult<AddressChangeRequest>> Create(CreateAddressChangeRequestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewAddress))
            {
                return BadRequest("Please enter the new address.");
            }
            if (request.NewAddress.Length > 500)
            {
                return BadRequest("Address must be 500 characters or fewer.");
            }
            if (request.EffectiveDate < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return BadRequest("Effective date can't be in the past.");
            }

            var family = await _familyRepository.GetByIdAsync(request.FamilyId);
            if (family is null)
            {
                return BadRequest($"FamilyId {request.FamilyId} does not exist.");
            }

            if (request.NewAreaId.HasValue)
            {
                var area = await _areaRepository.GetByIdAsync(request.NewAreaId.Value);
                if (area is null)
                {
                    return BadRequest($"AreaId {request.NewAreaId} does not exist.");
                }
            }

            var id = await _addressChangeRequestRepository.CreateAsync(request);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "AddressChangeRequested",
                EntityType = "AddressChangeRequest",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            var created = await _addressChangeRequestRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        /// <summary>Admin approves a Pending request -- applies the change to the family's
        /// Address/Area immediately and marks the request Approved.</summary>
        [HttpPut("{id}/approve")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Approve(ulong id, ReviewAddressChangeRequestRequest request)
        {
            var existing = await _addressChangeRequestRepository.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }
            if (existing.Status != AddressChangeRequestStatus.Pending)
            {
                return Conflict($"This request has already been {existing.Status}.");
            }

            var approved = await _addressChangeRequestRepository.ApproveAsync(id, request);
            if (!approved)
            {
                return Conflict("This request has already been reviewed.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ReviewedByAdminUserId,
                Action = "AddressChangeApproved",
                EntityType = "AddressChangeRequest",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        /// <summary>Admin rejects a Pending request -- no change to the family's record.</summary>
        [HttpPut("{id}/reject")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Reject(ulong id, ReviewAddressChangeRequestRequest request)
        {
            var existing = await _addressChangeRequestRepository.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound();
            }
            if (existing.Status != AddressChangeRequestStatus.Pending)
            {
                return Conflict($"This request has already been {existing.Status}.");
            }

            var rejected = await _addressChangeRequestRepository.RejectAsync(id, request);
            if (!rejected)
            {
                return Conflict("This request has already been reviewed.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ReviewedByAdminUserId,
                Action = "AddressChangeRejected",
                EntityType = "AddressChangeRequest",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        /// <summary>Family marks that they've seen the one-time flash about this request's outcome.</summary>
        [HttpPost("{id}/mark-notified")]
        public async Task<IActionResult> MarkNotified(ulong id)
        {
            await _addressChangeRequestRepository.MarkNotifiedAsync(id);
            return NoContent();
        }
    }
}
