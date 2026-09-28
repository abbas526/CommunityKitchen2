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
    public class ThaaliSizesController : ControllerBase
    {
        private readonly IThaaliSizeRepository _thaaliSizeRepository;

        public ThaaliSizesController(IThaaliSizeRepository thaaliSizeRepository)
        {
            _thaaliSizeRepository = thaaliSizeRepository;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ThaaliSize>>> GetAll()
        {
            return Ok(await _thaaliSizeRepository.GetAllAsync());
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ThaaliSize>> GetById(byte id)
        {
            var size = await _thaaliSizeRepository.GetByIdAsync(id);
            return size is null ? NotFound() : Ok(size);
        }

        [HttpPost]
        public async Task<ActionResult<ThaaliSize>> Create(CreateThaaliSizeRequest request)
        {
            var id = await _thaaliSizeRepository.CreateAsync(request);
            var created = await _thaaliSizeRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(byte id, UpdateThaaliSizeRequest request)
        {
            var updated = await _thaaliSizeRepository.UpdateAsync(id, request);
            return updated ? NoContent() : NotFound();
        }

        /// <summary>Only allowed while no family -- current or historical -- references this size, so deleting one never corrupts a report or a family's size history.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(byte id)
        {
            var size = await _thaaliSizeRepository.GetByIdAsync(id);
            if (size is null)
            {
                return NotFound();
            }

            if (await _thaaliSizeRepository.IsInUseAsync(id))
            {
                return Conflict($"'{size.Name}' can't be deleted -- it's already assigned to one or more families (current or historical).");
            }

            var deleted = await _thaaliSizeRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
