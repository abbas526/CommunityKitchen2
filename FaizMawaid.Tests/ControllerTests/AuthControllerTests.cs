using System.Security.Claims;
using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class AuthControllerTests
    {
        private static User SampleUser(ulong id = 1, byte roleId = 2, bool isActive = true, bool mustChangePassword = false, string hash = "hashed-password") => new()
        {
            Id = id,
            RoleId = roleId,
            Email = "fatema@example.com",
            FullName = "Fatema Rangwala",
            PasswordHash = hash,
            IsActive = isActive,
            MustChangePassword = mustChangePassword
        };

        private static AccessTokenResult SampleAccessToken() => new() { AccessToken = "sample.jwt.token", ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
        private static RefreshTokenResult SampleRefreshToken(string raw = "raw-refresh-token", string hash = "hashed-refresh-token") =>
            new() { RawToken = raw, TokenHash = hash, ExpiresAt = DateTime.UtcNow.AddDays(14) };

        private class Mocks
        {
            public Mock<IUserRepository> UserRepository { get; } = new();
            public Mock<IRoleRepository> RoleRepository { get; } = new();
            public Mock<IFamilyRepository> FamilyRepository { get; } = new();
            public Mock<IRefreshTokenRepository> RefreshTokenRepository { get; } = new();
            public Mock<IPasswordHasherService> PasswordHasher { get; } = new();
            public Mock<ITokenService> TokenService { get; } = new();
        }

        private static (AuthController controller, Mocks mocks) CreateController()
        {
            var mocks = new Mocks();
            mocks.RoleRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Role>
            {
                new() { Id = RoleIds.Admin, Name = RoleNames.Admin },
                new() { Id = RoleIds.FamilyHead, Name = RoleNames.FamilyHead }
            });
            mocks.TokenService.Setup(t => t.CreateAccessToken(It.IsAny<User>(), It.IsAny<string>())).Returns(SampleAccessToken());
            mocks.TokenService.Setup(t => t.CreateRefreshToken()).Returns(SampleRefreshToken());
            mocks.TokenService.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("hashed-refresh-token");

            var controller = new AuthController(
                mocks.UserRepository.Object,
                mocks.RoleRepository.Object,
                mocks.FamilyRepository.Object,
                mocks.RefreshTokenRepository.Object,
                mocks.PasswordHasher.Object,
                mocks.TokenService.Object);
            return (controller, mocks);
        }

        // ------------------------------------------------------------- Login

        [Fact]
        public async Task Login_ReturnsOkWithTokens_WhenCredentialsAreValid()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser();
            mocks.UserRepository.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            mocks.PasswordHasher.Setup(h => h.Verify(user.PasswordHash, "CorrectPass123")).Returns(true);
            mocks.FamilyRepository.Setup(r => r.GetByFamilyHeadUserIdAsync(user.Id)).ReturnsAsync(new Family { Id = 7, FamilyHeadUserId = user.Id });

            var result = await controller.Login(new LoginRequest { Email = user.Email, Password = "CorrectPass123" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<LoginResponse>(ok.Value);
            Assert.Equal("sample.jwt.token", response.AccessToken);
            Assert.Equal("raw-refresh-token", response.RefreshToken);
            Assert.Equal(user.Id, response.UserId);
            Assert.Equal(RoleNames.FamilyHead, response.RoleName);
            Assert.Equal(7UL, response.FamilyId);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenEmailNotFound()
        {
            var (controller, mocks) = CreateController();
            mocks.UserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

            var result = await controller.Login(new LoginRequest { Email = "nobody@example.com", Password = "whatever123" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser();
            mocks.UserRepository.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            mocks.PasswordHasher.Setup(h => h.Verify(user.PasswordHash, It.IsAny<string>())).Returns(false);

            var result = await controller.Login(new LoginRequest { Email = user.Email, Password = "WrongPassword" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenAccountIsDeactivated()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser(isActive: false);
            mocks.UserRepository.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            mocks.PasswordHasher.Setup(h => h.Verify(user.PasswordHash, It.IsAny<string>())).Returns(true);

            var result = await controller.Login(new LoginRequest { Email = user.Email, Password = "CorrectPass123" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        // ----------------------------------------------------------- Refresh

        [Fact]
        public async Task Refresh_ReturnsOkAndRotatesToken_WhenValid()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser();
            var stored = new RefreshToken { UserId = user.Id, TokenHash = "hashed-refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = null };
            mocks.TokenService.Setup(t => t.HashRefreshToken("presented-token")).Returns("hashed-refresh-token");
            mocks.RefreshTokenRepository.Setup(r => r.GetByTokenHashAsync("hashed-refresh-token")).ReturnsAsync(stored);
            mocks.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await controller.Refresh(new RefreshTokenRequest { RefreshToken = "presented-token" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<LoginResponse>(ok.Value);
            mocks.RefreshTokenRepository.Verify(r => r.RevokeAsync("hashed-refresh-token", "hashed-refresh-token"), Times.Once);
        }

        [Fact]
        public async Task Refresh_ReturnsUnauthorized_WhenTokenIsExpired()
        {
            var (controller, mocks) = CreateController();
            var stored = new RefreshToken { UserId = 1, TokenHash = "hashed-refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(-1), RevokedAt = null };
            mocks.TokenService.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("hashed-refresh-token");
            mocks.RefreshTokenRepository.Setup(r => r.GetByTokenHashAsync("hashed-refresh-token")).ReturnsAsync(stored);

            var result = await controller.Refresh(new RefreshTokenRequest { RefreshToken = "expired-token" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Refresh_ReturnsUnauthorized_WhenTokenIsRevoked()
        {
            var (controller, mocks) = CreateController();
            var stored = new RefreshToken { UserId = 1, TokenHash = "hashed-refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = DateTime.UtcNow.AddHours(-1) };
            mocks.TokenService.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("hashed-refresh-token");
            mocks.RefreshTokenRepository.Setup(r => r.GetByTokenHashAsync("hashed-refresh-token")).ReturnsAsync(stored);

            var result = await controller.Refresh(new RefreshTokenRequest { RefreshToken = "revoked-token" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Refresh_ReturnsUnauthorized_WhenTokenIsUnknown()
        {
            var (controller, mocks) = CreateController();
            mocks.TokenService.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("hashed-refresh-token");
            mocks.RefreshTokenRepository.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

            var result = await controller.Refresh(new RefreshTokenRequest { RefreshToken = "never-issued-token" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        // ---------------------------------------------------- BootstrapAdmin

        [Fact]
        public async Task BootstrapAdmin_CreatesFirstAdmin_WhenNoneExists()
        {
            var (controller, mocks) = CreateController();
            mocks.UserRepository.Setup(r => r.GetAllAsync(RoleIds.Admin)).ReturnsAsync(new List<User>());
            mocks.UserRepository.Setup(r => r.GetByEmailAsync("newadmin@example.com")).ReturnsAsync((User?)null);
            mocks.PasswordHasher.Setup(h => h.Hash("AdminPass123")).Returns("hashed-admin-password");
            var createdUser = SampleUser(id: 99, roleId: RoleIds.Admin, mustChangePassword: false);
            mocks.UserRepository.Setup(r => r.CreateAsync(It.IsAny<CreateUserRequest>())).ReturnsAsync(99UL);
            mocks.UserRepository.Setup(r => r.GetByIdAsync(99UL)).ReturnsAsync(createdUser);

            var result = await controller.BootstrapAdmin(new BootstrapAdminRequest
            {
                FullName = "First Admin",
                Email = "newadmin@example.com",
                Password = "AdminPass123"
            });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<LoginResponse>(ok.Value);
            Assert.Equal(RoleNames.Admin, response.RoleName);
            mocks.UserRepository.Verify(r => r.CreateAsync(It.Is<CreateUserRequest>(req =>
                req.RoleId == RoleIds.Admin && req.PasswordHash == "hashed-admin-password" && !req.MustChangePassword)), Times.Once);
        }

        [Fact]
        public async Task BootstrapAdmin_ReturnsConflict_WhenAnAdminAlreadyExists()
        {
            var (controller, mocks) = CreateController();
            mocks.UserRepository.Setup(r => r.GetAllAsync(RoleIds.Admin)).ReturnsAsync(new List<User> { SampleUser(roleId: RoleIds.Admin) });

            var result = await controller.BootstrapAdmin(new BootstrapAdminRequest
            {
                FullName = "Second Admin",
                Email = "second@example.com",
                Password = "AdminPass123"
            });

            Assert.IsType<ConflictObjectResult>(result.Result);
            mocks.UserRepository.Verify(r => r.CreateAsync(It.IsAny<CreateUserRequest>()), Times.Never);
        }

        [Fact]
        public async Task BootstrapAdmin_ReturnsBadRequest_WhenPasswordTooShort()
        {
            var (controller, mocks) = CreateController();
            mocks.UserRepository.Setup(r => r.GetAllAsync(RoleIds.Admin)).ReturnsAsync(new List<User>());

            var result = await controller.BootstrapAdmin(new BootstrapAdminRequest
            {
                FullName = "First Admin",
                Email = "newadmin@example.com",
                Password = "short"
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // -------------------------------------------------- ChangePassword

        private static void SetCallerIdentity(AuthController controller, ulong userId)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public async Task ChangePassword_ReturnsNoContent_WhenCurrentPasswordIsCorrect()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser(id: 5);
            SetCallerIdentity(controller, user.Id);
            mocks.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            mocks.PasswordHasher.Setup(h => h.Verify(user.PasswordHash, "OldPass123")).Returns(true);
            mocks.PasswordHasher.Setup(h => h.Hash("NewPass456")).Returns("hashed-new-password");

            var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "OldPass123", NewPassword = "NewPass456" });

            Assert.IsType<NoContentResult>(result);
            mocks.UserRepository.Verify(r => r.SetPasswordAsync(user.Id, "hashed-new-password", false), Times.Once);
        }

        [Fact]
        public async Task ChangePassword_ReturnsBadRequest_WhenCurrentPasswordIsWrong()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser(id: 5);
            SetCallerIdentity(controller, user.Id);
            mocks.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            mocks.PasswordHasher.Setup(h => h.Verify(user.PasswordHash, It.IsAny<string>())).Returns(false);

            var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "WrongOldPass", NewPassword = "NewPass456" });

            Assert.IsType<BadRequestObjectResult>(result);
            mocks.UserRepository.Verify(r => r.SetPasswordAsync(It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task ChangePassword_ReturnsUnauthorized_WhenNoCallerIdentityIsPresent()
        {
            var (controller, _) = CreateController();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "Whatever123", NewPassword = "NewPass456" });

            Assert.IsType<UnauthorizedResult>(result);
        }

        // ---------------------------------------------- AdminResetPassword

        [Fact]
        public async Task AdminResetPassword_ReturnsNoContent_WhenUserExists()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser(id: 8);
            mocks.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
            mocks.PasswordHasher.Setup(h => h.Hash("TempPass123")).Returns("hashed-temp-password");

            var result = await controller.AdminResetPassword(new AdminResetPasswordRequest { UserId = user.Id, NewPassword = "TempPass123" });

            Assert.IsType<NoContentResult>(result);
            mocks.UserRepository.Verify(r => r.SetPasswordAsync(user.Id, "hashed-temp-password", true), Times.Once);
        }

        [Fact]
        public async Task AdminResetPassword_ReturnsNotFound_WhenUserDoesNotExist()
        {
            var (controller, mocks) = CreateController();
            mocks.UserRepository.Setup(r => r.GetByIdAsync(It.IsAny<ulong>())).ReturnsAsync((User?)null);

            var result = await controller.AdminResetPassword(new AdminResetPasswordRequest { UserId = 999, NewPassword = "TempPass123" });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task AdminResetPassword_ReturnsBadRequest_WhenPasswordTooShort()
        {
            var (controller, mocks) = CreateController();
            var user = SampleUser(id: 8);
            mocks.UserRepository.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

            var result = await controller.AdminResetPassword(new AdminResetPasswordRequest { UserId = user.Id, NewPassword = "short" });

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
