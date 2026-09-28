using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    /// <summary>
    /// A private feedback/question thread between one family and the Admin team.
    /// Class-level attribute is a bare [Authorize] (any signed-in user) -- GetAll/
    /// GetOpenCount/Respond each carry their own explicit [Authorize(Roles = Admin)]
    /// so they stay Admin-only, following the pattern corrected on FamiliesController
    /// on 2026-09-26 (a class-level Roles=Admin combined with a method-level bare
    /// [Authorize] does NOT loosen it -- the class-level attribute must be the loose one).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public FeedbackController(IFeedbackRepository feedbackRepository, IAuditLogRepository auditLogRepository)
        {
            _feedbackRepository = feedbackRepository;
            _auditLogRepository = auditLogRepository;
        }

        /// <summary>Admin view -- every family's feedback, optionally filtered by status (e.g. ?status=Open).</summary>
        [HttpGet]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<IEnumerable<Feedback>>> GetAll([FromQuery] FeedbackStatus? status)
        {
            return Ok(await _feedbackRepository.GetAllAsync(status));
        }

        /// <summary>Admin dashboard tile -- how many feedback items are still waiting for a response.</summary>
        [HttpGet("open-count")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<int>> GetOpenCount()
        {
            return Ok(await _feedbackRepository.CountOpenAsync());
        }

        /// <summary>Family view -- their own feedback history (open and already-responded).</summary>
        [HttpGet("family/{familyId}")]
        public async Task<ActionResult<IEnumerable<Feedback>>> GetByFamily(ulong familyId)
        {
            return Ok(await _feedbackRepository.GetByFamilyIdAsync(familyId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Feedback>> GetById(ulong id)
        {
            var feedback = await _feedbackRepository.GetByIdAsync(id);
            return feedback is null ? NotFound() : Ok(feedback);
        }

        /// <summary>A Family Head submits a new piece of feedback/question for the Admin team.</summary>
        [HttpPost]
        public async Task<ActionResult<Feedback>> Create(CreateFeedbackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Please enter a message.");
            }
            if (request.Message.Length > 1000)
            {
                return BadRequest("Message must be 1000 characters or fewer.");
            }

            var id = await _feedbackRepository.CreateAsync(request);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "FeedbackSubmitted",
                EntityType = "Feedback",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            var created = await _feedbackRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        /// <summary>Admin adds (or revises) the response to a feedback item.</summary>
        [HttpPut("{id}/respond")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Respond(ulong id, RespondFeedbackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ResponseText))
            {
                return BadRequest("Please enter a response.");
            }
            if (request.ResponseText.Length > 1000)
            {
                return BadRequest("Response must be 1000 characters or fewer.");
            }

            var updated = await _feedbackRepository.RespondAsync(id, request);
            if (!updated)
            {
                return NotFound();
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.RespondedByAdminUserId,
                Action = "FeedbackResponded",
                EntityType = "Feedback",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }
    }
}
