using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // NOTE: deliberately a bare [Authorize] at class level, not [Authorize(Roles = Admin)] --
    // GetByArea below needs to be reachable by any signed-in Family, so every action that must
    // stay Admin-only carries its own explicit [Authorize(Roles = RoleNames.Admin)] (see the
    // FamiliesController/FeedbackController comment on this same gotcha).
    [Authorize]
    public class DeliveryPersonsController : ControllerBase
    {
        private readonly IDeliveryPersonRepository _deliveryPersonRepository;
        private readonly IAreaRepository _areaRepository;

        public DeliveryPersonsController(IDeliveryPersonRepository deliveryPersonRepository, IAreaRepository areaRepository)
        {
            _deliveryPersonRepository = deliveryPersonRepository;
            _areaRepository = areaRepository;
        }

        /// <summary>Every Delivery Person (active or not) -- for the Admin management page.</summary>
        [HttpGet]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<IEnumerable<DeliveryPerson>>> GetAll()
        {
            return Ok(await _deliveryPersonRepository.GetAllAsync());
        }

        [HttpGet("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<DeliveryPerson>> GetById(ulong id)
        {
            var person = await _deliveryPersonRepository.GetByIdAsync(id);
            return person is null ? NotFound() : Ok(person);
        }

        /// <summary>Active Delivery Persons for one Area -- what a family's dashboard shows for "who serves my area". Open to any signed-in user (any Family Head can look up any Area's server, same trust level as the rest of this app's family-facing lookups).</summary>
        [HttpGet("by-area/{areaId}")]
        public async Task<ActionResult<IEnumerable<DeliveryPerson>>> GetByArea(byte areaId)
        {
            return Ok(await _deliveryPersonRepository.GetActiveByAreaIdAsync(areaId));
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<DeliveryPerson>> Create(CreateDeliveryPersonRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.MobileNumber))
            {
                return BadRequest("Name and mobile number are both required.");
            }

            var area = await _areaRepository.GetByIdAsync(request.AreaId);
            if (area is null)
            {
                return BadRequest($"AreaId {request.AreaId} does not exist.");
            }

            var id = await _deliveryPersonRepository.CreateAsync(request);
            var created = await _deliveryPersonRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(ulong id, UpdateDeliveryPersonRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.MobileNumber))
            {
                return BadRequest("Name and mobile number are both required.");
            }

            var area = await _areaRepository.GetByIdAsync(request.AreaId);
            if (area is null)
            {
                return BadRequest($"AreaId {request.AreaId} does not exist.");
            }

            var updated = await _deliveryPersonRepository.UpdateAsync(id, request);
            return updated ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(ulong id)
        {
            var deleted = await _deliveryPersonRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
