using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace FaizMawaid.Controllers
{
    /// <summary>Admin-only library of reusable meal descriptions -- see MealPlanTemplate.cs.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    public class MealPlanTemplatesController : ControllerBase
    {
        private readonly IMealPlanTemplateRepository _mealPlanTemplateRepository;

        public MealPlanTemplatesController(IMealPlanTemplateRepository mealPlanTemplateRepository)
        {
            _mealPlanTemplateRepository = mealPlanTemplateRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MealPlanTemplate>>> GetAll()
        {
            return Ok(await _mealPlanTemplateRepository.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MealPlanTemplate>> GetById(ushort id)
        {
            var template = await _mealPlanTemplateRepository.GetByIdAsync(id);
            return template is null ? NotFound() : Ok(template);
        }

        [HttpPost]
        public async Task<ActionResult<MealPlanTemplate>> Create(CreateMealPlanTemplateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.MealDescription))
            {
                return BadRequest("A name and a meal description are both required.");
            }

            ushort id;
            try
            {
                id = await _mealPlanTemplateRepository.CreateAsync(request);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict($"A template named '{request.Name}' already exists.");
            }

            var created = await _mealPlanTemplateRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(ushort id, UpdateMealPlanTemplateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.MealDescription))
            {
                return BadRequest("A name and a meal description are both required.");
            }

            try
            {
                var updated = await _mealPlanTemplateRepository.UpdateAsync(id, request);
                return updated ? NoContent() : NotFound();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict($"A template named '{request.Name}' already exists.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(ushort id)
        {
            var deleted = await _mealPlanTemplateRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
