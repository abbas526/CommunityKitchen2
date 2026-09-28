using System.Security.Claims;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    /// <summary>
    /// Token-based auth (2026-09-24): login/refresh issue a JWT access token (short-lived,
    /// Jwt:AccessTokenMinutes) plus an opaque refresh token (long-lived, Jwt:RefreshTokenDays,
    /// stored only as a hash in RefreshTokens -- see Services/TokenService.cs). Password
    /// resets are Admin-initiated only (no email sender is configured) -- see
    /// AdminResetPassword and MustChangePassword on User.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly IFamilyRepository _familyRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordHasherService _passwordHasher;
        private readonly ITokenService _tokenService;

        public AuthController(
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            IFamilyRepository familyRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IPasswordHasherService passwordHasher,
            ITokenService tokenService)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _familyRepository = familyRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            // Deliberately the same message whether the email doesn't exist or the
            // password is wrong -- never reveal which one it was.
            if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
            {
                return Unauthorized("Incorrect email or password.");
            }
            if (!user.IsActive)
            {
                return Unauthorized("This account has been deactivated. Please contact the Kitchen Admin.");
            }

            var roleName = await ResolveRoleNameAsync(user.RoleId);
            return Ok(await IssueTokensAsync(user, roleName));
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Refresh(RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Unauthorized("Your session has expired. Please log in again.");
            }

            var hash = _tokenService.HashRefreshToken(request.RefreshToken);
            var existing = await _refreshTokenRepository.GetByTokenHashAsync(hash);
            if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt < DateTime.UtcNow)
            {
                return Unauthorized("Your session has expired. Please log in again.");
            }

            var user = await _userRepository.GetByIdAsync(existing.UserId);
            if (user is null || !user.IsActive)
            {
                return Unauthorized("This account is no longer active.");
            }

            var roleName = await ResolveRoleNameAsync(user.RoleId);
            var response = await IssueTokensAsync(user, roleName);
            // Rotate: the old refresh token is now invalid, replaced by the new one.
            await _refreshTokenRepository.RevokeAsync(hash, _tokenService.HashRefreshToken(response.RefreshToken));
            return Ok(response);
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout(RefreshTokenRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _refreshTokenRepository.RevokeAsync(_tokenService.HashRefreshToken(request.RefreshToken));
            }
            return NoContent();
        }

        /// <summary>
        /// One-time bootstrap for a brand-new install: creates the very first Admin
        /// account. Self-limiting -- refuses once ANY Admin already exists, since at that
        /// point there's a real account able to create further Admins through the normal
        /// (authenticated) UsersController.Create instead.
        /// </summary>
        [HttpPost("bootstrap-admin")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> BootstrapAdmin(BootstrapAdminRequest request)
        {
            var existingAdmins = await _userRepository.GetAllAsync(RoleIds.Admin);
            if (existingAdmins.Any())
            {
                return Conflict("An Admin account already exists. Ask an existing Admin to create your account instead.");
            }
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                return BadRequest("Password must be at least 8 characters.");
            }
            var existingEmail = await _userRepository.GetByEmailAsync(request.Email);
            if (existingEmail is not null)
            {
                return Conflict($"A user with email '{request.Email}' already exists.");
            }

            var userId = await _userRepository.CreateAsync(new CreateUserRequest
            {
                RoleId = RoleIds.Admin,
                Email = request.Email,
                FullName = request.FullName,
                Phone = request.Phone,
                PasswordHash = _passwordHasher.Hash(request.Password),
                MustChangePassword = false // they just chose it themselves
            });
            var user = await _userRepository.GetByIdAsync(userId);

            return Ok(await IssueTokensAsync(user!, RoleNames.Admin));
        }

        /// <summary>Self-service: the caller changes their own password, proving they know the current one. Reads the caller's identity from the validated JWT -- never trust a client-supplied user id for this.</summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null || !ulong.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
            {
                return Unauthorized();
            }
            if (!_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
            {
                return BadRequest("Current password is incorrect.");
            }
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return BadRequest("New password must be at least 8 characters.");
            }

            await _userRepository.SetPasswordAsync(userId, _passwordHasher.Hash(request.NewPassword), mustChangePassword: false);
            return NoContent();
        }

        /// <summary>Admin-only: sets a temporary password for any user (per the community's choice of admin-initiated resets over an email flow -- no SMTP is configured). Forces MustChangePassword so the real owner sets their own at next login.</summary>
        [HttpPost("admin-reset-password")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> AdminResetPassword(AdminResetPasswordRequest request)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user is null)
            {
                return NotFound();
            }
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return BadRequest("New password must be at least 8 characters.");
            }

            await _userRepository.SetPasswordAsync(request.UserId, _passwordHasher.Hash(request.NewPassword), mustChangePassword: true);
            return NoContent();
        }

        private async Task<string> ResolveRoleNameAsync(byte roleId)
        {
            var roles = await _roleRepository.GetAllAsync();
            return roles.FirstOrDefault(r => r.Id == roleId)?.Name ?? RoleNames.FamilyHead;
        }

        private async Task<LoginResponse> IssueTokensAsync(User user, string roleName)
        {
            var access = _tokenService.CreateAccessToken(user, roleName);
            var refresh = _tokenService.CreateRefreshToken();
            var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString();
            await _refreshTokenRepository.CreateAsync(user.Id, refresh.TokenHash, refresh.ExpiresAt, ip);

            ulong? familyId = null;
            if (roleName == RoleNames.FamilyHead)
            {
                var family = await _familyRepository.GetByFamilyHeadUserIdAsync(user.Id);
                familyId = family?.Id;
            }

            return new LoginResponse
            {
                AccessToken = access.AccessToken,
                AccessTokenExpiresAt = access.ExpiresAt,
                RefreshToken = refresh.RawToken,
                UserId = user.Id,
                RoleId = user.RoleId,
                RoleName = roleName,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                SabilNumber = user.SabilNumber,
                FamilyId = familyId,
                MustChangePassword = user.MustChangePassword
            };
        }
    }
}
