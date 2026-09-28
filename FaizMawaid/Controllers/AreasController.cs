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
    public class AreasController : ControllerBase
    {
        private readonly IAreaRepository _areaRepository;

        public AreasController(IAreaRepository areaRepository)
        {
            _areaRepository = areaRepository;
        }

        /// <summary>Anonymous -- the family registration page needs this list before anyone is signed in.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<Area>>> GetAll()
        {
            return Ok(await _areaRepository.GetAllAsync());
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<Area>> GetById(byte id)
        {
            var area = await _areaRepository.GetByIdAsync(id);
            return area is null ? NotFound() : Ok(area);
        }

        [HttpPost]
        public async Task<ActionResult<Area>> Create(CreateAreaRequest request)
        {
            var id = await _areaRepository.CreateAsync(request);
            var created = await _areaRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(byte id, UpdateAreaRequest request)
        {
            var updated = await _areaRepository.UpdateAsync(id, request);
            return updated ? NoContent() : NotFound();
        }

        /// <summary>Only allowed while no family or Delivery Person references this Area.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(byte id)
        {
            var area = await _areaRepository.GetByIdAsync(id);
            if (area is null)
            {
                return NotFound();
            }

            if (await _areaRepository.IsInUseAsync(id))
            {
                return Conflict($"'{area.Name}' can't be deleted -- it's still assigned to one or more families or Delivery Persons.");
            }

            var deleted = await _areaRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
