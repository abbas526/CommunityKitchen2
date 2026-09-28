using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace FaizMawaid.Controllers
{
    /// <summary>Admin-only user management -- creating an account directly (rather than via family self-registration), editing profile fields, and activate/deactivate. Password resets live on AuthController (admin-reset-password), not here.</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasherService _passwordHasher;

        public UsersController(IUserRepository userRepository, IPasswordHasherService passwordHasher)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll([FromQuery] byte? roleId)
        {
            return Ok(await _userRepository.GetAllAsync(roleId));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetById(ulong id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            return user is null ? NotFound() : Ok(user);
        }

        /// <summary>Creates a login account directly (e.g. a new Admin, or a Family Head added by an Admin rather than via self-registration). The Admin chooses the initial password; the real owner is forced to change it at first login.</summary>
        [HttpPost]
        public async Task<ActionResult<User>> Create(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                return BadRequest("Password must be at least 8 characters.");
            }

            var existing = await _userRepository.GetByEmailAsync(request.Email);
            if (existing is not null)
            {
                return Conflict($"A user with email '{request.Email}' already exists.");
            }

            if (!string.IsNullOrWhiteSpace(request.SabilNumber))
            {
                var existingSabil = await _userRepository.GetBySabilNumberAsync(request.SabilNumber);
                if (existingSabil is not null)
                {
                    return Conflict($"Sabil Number '{request.SabilNumber}' is already registered to another user.");
                }
            }

            request.PasswordHash = _passwordHasher.Hash(request.Password);
            request.MustChangePassword = true;

            var id = await _userRepository.CreateAsync(request);
            var created = await _userRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(ulong id, UpdateUserRequest request)
        {
            try
            {
                var updated = await _userRepository.UpdateAsync(id, request);
                return updated ? NoContent() : NotFound();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return Conflict($"Sabil Number '{request.SabilNumber}' is already registered to another user.");
            }
        }

        [HttpPut("{id}/activate")]
        public async Task<IActionResult> Activate(ulong id)
        {
            var updated = await _userRepository.SetActiveAsync(id, true);
            return updated ? NoContent() : NotFound();
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(ulong id)
        {
            var updated = await _userRepository.SetActiveAsync(id, false);
            return updated ? NoContent() : NotFound();
        }
    }
}
