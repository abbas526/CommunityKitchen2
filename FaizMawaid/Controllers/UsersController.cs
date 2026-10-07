using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace FaizMawaid.Controllers
{
    /// <summary>
    /// Admin-only user management -- creating an account directly (rather than via family self-registration), editing profile fields, and activate/deactivate. Password resets live on AuthController (admin-reset-password), not here.
    /// SuperAdmin hierarchy (2026-10-07): a regular Admin can only manage Family Heads; only a SuperAdmin can create or
    /// change Admin/SuperAdmin accounts (see Services/AccountHierarchy.cs), and at most 2 ACTIVE SuperAdmins may exist.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public UsersController(
            IUserRepository userRepository,
            IPasswordHasherService passwordHasher,
            IAuditLogRepository auditLogRepository,
            IRefreshTokenRepository refreshTokenRepository)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _auditLogRepository = auditLogRepository;
            _refreshTokenRepository = refreshTokenRepository;
        }

        private ObjectResult Forbidden(string message) => StatusCode(StatusCodes.Status403Forbidden, message);

        private async Task AuditAsync(ulong actorId, string action, ulong entityId, object? metadata = null)
        {
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = actorId,
                Action = action,
                EntityType = "User",
                EntityId = entityId,
                MetadataJson = metadata is null ? null : System.Text.Json.JsonSerializer.Serialize(metadata),
                CreatedAt = DateTime.UtcNow
            });
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

        /// <summary>Creates a login account directly (e.g. a new Admin, or a Family Head added by an Admin rather than via self-registration). The Admin chooses the initial password; the real owner is forced to change it at first login. Only a SuperAdmin may create an Admin or SuperAdmin account (a SuperAdmin only while fewer than 2 active ones exist).</summary>
        [HttpPost]
        public async Task<ActionResult<User>> Create(CreateUserRequest request)
        {
            if (!AccountHierarchy.TryGetCaller(User, out var callerId, out var callerIsSuperAdmin))
            {
                return Unauthorized();
            }
            if (request.RoleId != RoleIds.FamilyHead && request.RoleId != RoleIds.Admin && request.RoleId != RoleIds.SuperAdmin)
            {
                return BadRequest("Unknown role.");
            }
            if (!AccountHierarchy.CanManage(callerIsSuperAdmin, request.RoleId))
            {
                return Forbidden(AccountHierarchy.OnlySuperAdminMessage);
            }

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

            ulong id;
            if (request.RoleId == RoleIds.SuperAdmin)
            {
                var created = await _userRepository.CreateSuperAdminAsync(request);
                if (created.Result == UserChangeResult.SeatsFull)
                {
                    return Conflict(AccountHierarchy.SeatsFullMessage);
                }
                id = created.UserId;
            }
            else
            {
                id = await _userRepository.CreateAsync(request);
            }

            await AuditAsync(callerId, "UserCreated", id, new { request.RoleId, request.Email });

            var createdUser = await _userRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, createdUser);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(ulong id, UpdateUserRequest request)
        {
            if (!AccountHierarchy.TryGetCaller(User, out var callerId, out var callerIsSuperAdmin))
            {
                return Unauthorized();
            }
            var target = await _userRepository.GetByIdAsync(id);
            if (target is null)
            {
                return NotFound();
            }
            // Anyone may edit their OWN name/phone; otherwise the hierarchy applies.
            if (target.Id != callerId && !AccountHierarchy.CanManage(callerIsSuperAdmin, target.RoleId))
            {
                return Forbidden(AccountHierarchy.OnlySuperAdminMessage);
            }

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
        public Task<IActionResult> Activate(ulong id) => SetActiveAsync(id, true);

        [HttpPut("{id}/deactivate")]
        public Task<IActionResult> Deactivate(ulong id) => SetActiveAsync(id, false);

        private async Task<IActionResult> SetActiveAsync(ulong id, bool isActive)
        {
            if (!AccountHierarchy.TryGetCaller(User, out var callerId, out var callerIsSuperAdmin))
            {
                return Unauthorized();
            }
            var target = await _userRepository.GetByIdAsync(id);
            if (target is null)
            {
                return NotFound();
            }
            if (!AccountHierarchy.CanManage(callerIsSuperAdmin, target.RoleId))
            {
                return Forbidden(AccountHierarchy.OnlySuperAdminMessage);
            }

            var result = await _userRepository.SetActiveGuardedAsync(id, isActive);
            switch (result)
            {
                case UserChangeResult.NotFound:
                    return NotFound();
                case UserChangeResult.SeatsFull:
                    return Conflict(AccountHierarchy.SeatsFullMessage);
                case UserChangeResult.WouldLeaveNoSuperAdmin:
                    return Conflict(AccountHierarchy.LastSuperAdminMessage);
            }

            if (!isActive)
            {
                // A deactivated account must not be able to keep refreshing an existing session.
                await _refreshTokenRepository.RevokeAllForUserAsync(id);
            }
            await AuditAsync(callerId, isActive ? "UserActivated" : "UserDeactivated", id, new { target.RoleId });
            return NoContent();
        }

        /// <summary>SuperAdmin-only: moves an Admin or SuperAdmin between those two roles. Never your own role; never below 1 active SuperAdmin; never above 2.</summary>
        [HttpPut("{id}/role")]
        [Authorize(Roles = RoleNames.SuperAdmin)]
        public async Task<IActionResult> ChangeRole(ulong id, ChangeUserRoleRequest request)
        {
            if (!AccountHierarchy.TryGetCaller(User, out var callerId, out var callerIsSuperAdmin) || !callerIsSuperAdmin)
            {
                return Unauthorized();
            }
            if (id == callerId)
            {
                return BadRequest("You can't change your own role.");
            }
            if (request.RoleId != RoleIds.Admin && request.RoleId != RoleIds.SuperAdmin)
            {
                return BadRequest("A role can only be changed between Admin and SuperAdmin.");
            }

            var target = await _userRepository.GetByIdAsync(id);
            if (target is null)
            {
                return NotFound();
            }
            if (target.RoleId != RoleIds.Admin && target.RoleId != RoleIds.SuperAdmin)
            {
                return BadRequest("Only Admin and SuperAdmin accounts can have their role changed here.");
            }

            var result = await _userRepository.SetRoleAsync(id, request.RoleId);
            switch (result)
            {
                case UserChangeResult.NotFound:
                    return NotFound();
                case UserChangeResult.SeatsFull:
                    return Conflict(AccountHierarchy.SeatsFullMessage);
                case UserChangeResult.WouldLeaveNoSuperAdmin:
                    return Conflict(AccountHierarchy.LastSuperAdminMessage);
            }

            if (target.RoleId != request.RoleId)
            {
                if (request.RoleId == RoleIds.Admin)
                {
                    // Demotion: make the old (higher-privilege) session unable to refresh.
                    await _refreshTokenRepository.RevokeAllForUserAsync(id);
                }
                await AuditAsync(callerId, "UserRoleChanged", id, new { From = target.RoleId, To = request.RoleId });
            }
            return NoContent();
        }

        /// <summary>SuperAdmin-only: how many of the allowed SuperAdmin seats are in use (active SuperAdmins only) -- drives the "N of 2" indicator on the Manage Admins page.</summary>
        [HttpGet("superadmin-seats")]
        [Authorize(Roles = RoleNames.SuperAdmin)]
        public async Task<ActionResult<SuperAdminSeatsResponse>> GetSuperAdminSeats()
        {
            var used = await _userRepository.CountActiveByRoleAsync(RoleIds.SuperAdmin);
            return Ok(new SuperAdminSeatsResponse { Used = used, Max = RoleIds.MaxActiveSuperAdmins });
        }
    }
}
