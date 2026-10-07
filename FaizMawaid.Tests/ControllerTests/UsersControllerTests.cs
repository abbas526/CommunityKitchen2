using System.Reflection;
using System.Security.Claims;
using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class UsersControllerTests
    {
        private const ulong CallerId = 1;

        private class Mocks
        {
            public Mock<IUserRepository> Users { get; } = new();
            public Mock<IPasswordHasherService> Hasher { get; } = new();
            public Mock<IAuditLogRepository> Audit { get; } = new();
            public Mock<IRefreshTokenRepository> RefreshTokens { get; } = new();
        }

        /// <summary>Builds the controller with the caller's identity in the claims (as the JWT would provide), defaulting to a regular Admin.</summary>
        private static (UsersController controller, Mocks mocks) Create(bool callerIsSuperAdmin = false, bool withIdentity = true, ulong callerId = CallerId)
        {
            var mocks = new Mocks();
            mocks.Hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
            var controller = new UsersController(mocks.Users.Object, mocks.Hasher.Object, mocks.Audit.Object, mocks.RefreshTokens.Object);
            if (withIdentity)
            {
                // A SuperAdmin's real token carries BOTH role claims.
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, callerId.ToString()),
                    new(ClaimTypes.Role, RoleNames.Admin)
                };
                if (callerIsSuperAdmin) { claims.Add(new Claim(ClaimTypes.Role, RoleNames.SuperAdmin)); }
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
                };
            }
            return (controller, mocks);
        }

        private static User UserWithRole(ulong id, byte roleId, bool isActive = true) => new()
        {
            Id = id,
            RoleId = roleId,
            Email = $"user{id}@example.com",
            FullName = $"User {id}",
            IsActive = isActive
        };

        private static void AssertForbidden(IActionResult? result)
        {
            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
        }

        // ------------------------------------------------------------ existing behaviour

        [Fact]
        public async Task GetAll_ReturnsOkWithUsers()
        {
            var (controller, mocks) = Create();
            var users = new List<User> { new() { Id = 1, RoleId = RoleIds.Admin, Email = "a@b.com", FullName = "A B" } };
            mocks.Users.Setup(r => r.GetAllAsync(null)).ReturnsAsync(users);

            var result = await controller.GetAll(null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(users, okResult.Value);
        }

        [Fact]
        public async Task GetById_ReturnsOkWhenUserExists()
        {
            var (controller, mocks) = Create();
            var user = new User { Id = 5, RoleId = RoleIds.Admin, Email = "a@b.com", FullName = "A B" };
            mocks.Users.Setup(r => r.GetByIdAsync(5UL)).ReturnsAsync(user);

            var result = await controller.GetById(5);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(user, okResult.Value);
        }

        [Fact]
        public async Task Create_FamilyHead_ByRegularAdmin_ReturnsCreatedAtAction()
        {
            var (controller, mocks) = Create();
            var request = new CreateUserRequest { RoleId = RoleIds.FamilyHead, Email = "new@b.com", Password = "TestPass123", FullName = "New Head" };
            mocks.Users.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);
            mocks.Users.Setup(r => r.CreateAsync(request)).ReturnsAsync(42UL);
            var created = UserWithRole(42, RoleIds.FamilyHead);
            mocks.Users.Setup(r => r.GetByIdAsync(42UL)).ReturnsAsync(created);

            var result = await controller.Create(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
            Assert.True(request.MustChangePassword);
            mocks.Audit.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.UserId == CallerId && l.Action == "UserCreated" && l.EntityId == 42UL)), Times.Once);
        }

        [Fact]
        public async Task Create_ReturnsBadRequestWhenPasswordIsTooShort()
        {
            var (controller, _) = Create();
            var request = new CreateUserRequest { RoleId = RoleIds.FamilyHead, Email = "new@b.com", Password = "short", FullName = "New Head" };

            var result = await controller.Create(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Create_ReturnsBadRequest_ForAnUnknownRole()
        {
            var (controller, _) = Create(callerIsSuperAdmin: true);
            var request = new CreateUserRequest { RoleId = 9, Email = "new@b.com", Password = "TestPass123", FullName = "X" };

            var result = await controller.Create(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Create_ReturnsUnauthorized_WhenCallerIdentityIsMissing()
        {
            var (controller, _) = Create(withIdentity: false);
            var request = new CreateUserRequest { RoleId = RoleIds.FamilyHead, Email = "new@b.com", Password = "TestPass123", FullName = "X" };

            var result = await controller.Create(request);

            Assert.IsType<UnauthorizedResult>(result.Result);
        }

        [Fact]
        public async Task Update_ReturnsNoContentWhenUpdated()
        {
            var (controller, mocks) = Create();
            var request = new UpdateUserRequest { FullName = "Updated Name" };
            mocks.Users.Setup(r => r.GetByIdAsync(7UL)).ReturnsAsync(UserWithRole(7, RoleIds.FamilyHead));
            mocks.Users.Setup(r => r.UpdateAsync(7UL, request)).ReturnsAsync(true);

            var result = await controller.Update(7, request);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Deactivate_FamilyHead_ReturnsNoContent_RevokesTokens_AndAudits()
        {
            var (controller, mocks) = Create();
            mocks.Users.Setup(r => r.GetByIdAsync(3UL)).ReturnsAsync(UserWithRole(3, RoleIds.FamilyHead));
            mocks.Users.Setup(r => r.SetActiveGuardedAsync(3UL, false)).ReturnsAsync(UserChangeResult.Ok);

            var result = await controller.Deactivate(3);

            Assert.IsType<NoContentResult>(result);
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(3UL), Times.Once);
            mocks.Audit.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "UserDeactivated" && l.EntityId == 3UL)), Times.Once);
        }

        [Fact]
        public async Task Activate_FamilyHead_ReturnsNoContent_AndDoesNotRevokeTokens()
        {
            var (controller, mocks) = Create();
            mocks.Users.Setup(r => r.GetByIdAsync(3UL)).ReturnsAsync(UserWithRole(3, RoleIds.FamilyHead, isActive: false));
            mocks.Users.Setup(r => r.SetActiveGuardedAsync(3UL, true)).ReturnsAsync(UserChangeResult.Ok);

            var result = await controller.Activate(3);

            Assert.IsType<NoContentResult>(result);
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(It.IsAny<ulong>()), Times.Never);
        }

        // ------------------------------------------------------------ hierarchy: Create

        [Theory]
        [InlineData(RoleIds.Admin)]
        [InlineData(RoleIds.SuperAdmin)]
        public async Task Create_RegularAdmin_CannotCreateAdminOrSuperAdmin(byte roleId)
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: false);
            var request = new CreateUserRequest { RoleId = roleId, Email = "x@b.com", Password = "TestPass123", FullName = "X" };

            var result = await controller.Create(request);

            AssertForbidden(result.Result);
            mocks.Users.Verify(r => r.CreateAsync(It.IsAny<CreateUserRequest>()), Times.Never);
            mocks.Users.Verify(r => r.CreateSuperAdminAsync(It.IsAny<CreateUserRequest>()), Times.Never);
        }

        [Fact]
        public async Task Create_SuperAdmin_CanCreateAdmin()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            var request = new CreateUserRequest { RoleId = RoleIds.Admin, Email = "adm@b.com", Password = "TestPass123", FullName = "Adm" };
            mocks.Users.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);
            mocks.Users.Setup(r => r.CreateAsync(request)).ReturnsAsync(50UL);
            mocks.Users.Setup(r => r.GetByIdAsync(50UL)).ReturnsAsync(UserWithRole(50, RoleIds.Admin));

            var result = await controller.Create(request);

            Assert.IsType<CreatedAtActionResult>(result.Result);
            mocks.Users.Verify(r => r.CreateSuperAdminAsync(It.IsAny<CreateUserRequest>()), Times.Never);
        }

        [Fact]
        public async Task Create_SuperAdmin_CanCreateASecondSuperAdmin_ViaTheSeatGuardedPath()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            var request = new CreateUserRequest { RoleId = RoleIds.SuperAdmin, Email = "sa2@b.com", Password = "TestPass123", FullName = "SA Two" };
            mocks.Users.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);
            mocks.Users.Setup(r => r.CreateSuperAdminAsync(request)).ReturnsAsync(new UserCreateResult { Result = UserChangeResult.Ok, UserId = 51 });
            mocks.Users.Setup(r => r.GetByIdAsync(51UL)).ReturnsAsync(UserWithRole(51, RoleIds.SuperAdmin));

            var result = await controller.Create(request);

            Assert.IsType<CreatedAtActionResult>(result.Result);
            // A SuperAdmin must NEVER be created through the unguarded insert.
            mocks.Users.Verify(r => r.CreateAsync(It.IsAny<CreateUserRequest>()), Times.Never);
        }

        [Fact]
        public async Task Create_SuperAdmin_ReturnsConflict_WhenBothSeatsAreInUse()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            var request = new CreateUserRequest { RoleId = RoleIds.SuperAdmin, Email = "sa3@b.com", Password = "TestPass123", FullName = "SA Three" };
            mocks.Users.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);
            mocks.Users.Setup(r => r.CreateSuperAdminAsync(request)).ReturnsAsync(new UserCreateResult { Result = UserChangeResult.SeatsFull });

            var result = await controller.Create(request);

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(AccountHierarchy.SeatsFullMessage, conflict.Value);
            mocks.Audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
        }

        // ------------------------------------------------------------ hierarchy: Update / Activate / Deactivate

        [Theory]
        [InlineData(RoleIds.Admin)]
        [InlineData(RoleIds.SuperAdmin)]
        public async Task Update_RegularAdmin_CannotEditAnotherAdminOrSuperAdmin(byte targetRole)
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: false);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, targetRole));

            var result = await controller.Update(9, new UpdateUserRequest { FullName = "Hacked" });

            AssertForbidden(result);
            mocks.Users.Verify(r => r.UpdateAsync(It.IsAny<ulong>(), It.IsAny<UpdateUserRequest>()), Times.Never);
        }

        [Fact]
        public async Task Update_RegularAdmin_CanEditTheirOwnProfile()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: false, callerId: 4);
            var request = new UpdateUserRequest { FullName = "Me" };
            mocks.Users.Setup(r => r.GetByIdAsync(4UL)).ReturnsAsync(UserWithRole(4, RoleIds.Admin));
            mocks.Users.Setup(r => r.UpdateAsync(4UL, request)).ReturnsAsync(true);

            var result = await controller.Update(4, request);

            Assert.IsType<NoContentResult>(result);
        }

        [Theory]
        [InlineData(RoleIds.Admin)]
        [InlineData(RoleIds.SuperAdmin)]
        public async Task Deactivate_RegularAdmin_CannotDeactivateAdminOrSuperAdmin(byte targetRole)
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: false);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, targetRole));

            var result = await controller.Deactivate(9);

            AssertForbidden(result);
            mocks.Users.Verify(r => r.SetActiveGuardedAsync(It.IsAny<ulong>(), It.IsAny<bool>()), Times.Never);
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(It.IsAny<ulong>()), Times.Never);
        }

        [Theory]
        [InlineData(RoleIds.Admin)]
        [InlineData(RoleIds.SuperAdmin)]
        public async Task Activate_RegularAdmin_CannotActivateAdminOrSuperAdmin(byte targetRole)
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: false);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, targetRole, isActive: false));

            var result = await controller.Activate(9);

            AssertForbidden(result);
            mocks.Users.Verify(r => r.SetActiveGuardedAsync(It.IsAny<ulong>(), It.IsAny<bool>()), Times.Never);
        }

        [Theory]
        [InlineData(RoleIds.FamilyHead)]
        [InlineData(RoleIds.Admin)]
        [InlineData(RoleIds.SuperAdmin)]
        public async Task Deactivate_SuperAdmin_CanDeactivateAnyone(byte targetRole)
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, targetRole));
            mocks.Users.Setup(r => r.SetActiveGuardedAsync(9UL, false)).ReturnsAsync(UserChangeResult.Ok);

            var result = await controller.Deactivate(9);

            Assert.IsType<NoContentResult>(result);
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(9UL), Times.Once);
        }

        [Fact]
        public async Task Deactivate_ReturnsConflict_WhenItWouldLeaveNoActiveSuperAdmin()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.SuperAdmin));
            mocks.Users.Setup(r => r.SetActiveGuardedAsync(9UL, false)).ReturnsAsync(UserChangeResult.WouldLeaveNoSuperAdmin);

            var result = await controller.Deactivate(9);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(AccountHierarchy.LastSuperAdminMessage, conflict.Value);
            // A refused deactivation must not sign the account out or leave an audit entry.
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(It.IsAny<ulong>()), Times.Never);
            mocks.Audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
        }

        [Fact]
        public async Task Activate_ReturnsConflict_WhenReactivatingASuperAdminWouldExceedTheCap()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.SuperAdmin, isActive: false));
            mocks.Users.Setup(r => r.SetActiveGuardedAsync(9UL, true)).ReturnsAsync(UserChangeResult.SeatsFull);

            var result = await controller.Activate(9);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(AccountHierarchy.SeatsFullMessage, conflict.Value);
        }

        [Fact]
        public async Task Deactivate_ReturnsNotFound_WhenTargetDoesNotExist()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(It.IsAny<ulong>())).ReturnsAsync((User?)null);

            var result = await controller.Deactivate(77);

            Assert.IsType<NotFoundResult>(result);
        }

        // ------------------------------------------------------------ ChangeRole

        [Fact]
        public async Task ChangeRole_PromoteAdminToSuperAdmin_Succeeds_AndAudits()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.Admin));
            mocks.Users.Setup(r => r.SetRoleAsync(9UL, RoleIds.SuperAdmin)).ReturnsAsync(UserChangeResult.Ok);

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.SuperAdmin });

            Assert.IsType<NoContentResult>(result);
            mocks.Audit.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "UserRoleChanged" && l.EntityId == 9UL)), Times.Once);
            // Promotion doesn't need to sign anyone out.
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public async Task ChangeRole_DemoteSuperAdminToAdmin_RevokesTheirRefreshTokens()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.SuperAdmin));
            mocks.Users.Setup(r => r.SetRoleAsync(9UL, RoleIds.Admin)).ReturnsAsync(UserChangeResult.Ok);

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.Admin });

            Assert.IsType<NoContentResult>(result);
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(9UL), Times.Once);
        }

        [Fact]
        public async Task ChangeRole_RefusesToChangeYourOwnRole()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true, callerId: 5);

            var result = await controller.ChangeRole(5, new ChangeUserRoleRequest { RoleId = RoleIds.Admin });

            Assert.IsType<BadRequestObjectResult>(result);
            mocks.Users.Verify(r => r.SetRoleAsync(It.IsAny<ulong>(), It.IsAny<byte>()), Times.Never);
        }

        [Fact]
        public async Task ChangeRole_RefusesFamilyHeadAsTheNewRole()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.FamilyHead });

            Assert.IsType<BadRequestObjectResult>(result);
            mocks.Users.Verify(r => r.SetRoleAsync(It.IsAny<ulong>(), It.IsAny<byte>()), Times.Never);
        }

        [Fact]
        public async Task ChangeRole_RefusesToTouchAFamilyHeadAccount()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.FamilyHead));

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.SuperAdmin });

            Assert.IsType<BadRequestObjectResult>(result);
            mocks.Users.Verify(r => r.SetRoleAsync(It.IsAny<ulong>(), It.IsAny<byte>()), Times.Never);
        }

        [Fact]
        public async Task ChangeRole_ReturnsConflict_WhenSeatsAreFull()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.Admin));
            mocks.Users.Setup(r => r.SetRoleAsync(9UL, RoleIds.SuperAdmin)).ReturnsAsync(UserChangeResult.SeatsFull);

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.SuperAdmin });

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(AccountHierarchy.SeatsFullMessage, conflict.Value);
        }

        [Fact]
        public async Task ChangeRole_ReturnsConflict_WhenDemotingTheLastActiveSuperAdmin()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(UserWithRole(9, RoleIds.SuperAdmin));
            mocks.Users.Setup(r => r.SetRoleAsync(9UL, RoleIds.Admin)).ReturnsAsync(UserChangeResult.WouldLeaveNoSuperAdmin);

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.Admin });

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(AccountHierarchy.LastSuperAdminMessage, conflict.Value);
            mocks.RefreshTokens.Verify(r => r.RevokeAllForUserAsync(It.IsAny<ulong>()), Times.Never);
        }

        [Fact]
        public async Task ChangeRole_ReturnsUnauthorized_WhenCallerIsNotASuperAdmin()
        {
            // Defence in depth: even if the [Authorize] attribute were removed, a regular Admin gets nothing.
            var (controller, mocks) = Create(callerIsSuperAdmin: false);

            var result = await controller.ChangeRole(9, new ChangeUserRoleRequest { RoleId = RoleIds.SuperAdmin });

            Assert.IsType<UnauthorizedResult>(result);
            mocks.Users.Verify(r => r.SetRoleAsync(It.IsAny<ulong>(), It.IsAny<byte>()), Times.Never);
        }

        // ------------------------------------------------------------ seats

        [Fact]
        public async Task GetSuperAdminSeats_ReportsActiveCountAndTheMax()
        {
            var (controller, mocks) = Create(callerIsSuperAdmin: true);
            mocks.Users.Setup(r => r.CountActiveByRoleAsync(RoleIds.SuperAdmin)).ReturnsAsync(1);

            var result = await controller.GetSuperAdminSeats();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var seats = Assert.IsType<SuperAdminSeatsResponse>(ok.Value);
            Assert.Equal(1, seats.Used);
            Assert.Equal(2, seats.Max);
        }

        // ------------------------------------------------------------ attributes (Moq bypasses [Authorize], so check them by reflection)

        [Theory]
        [InlineData(nameof(UsersController.ChangeRole))]
        [InlineData(nameof(UsersController.GetSuperAdminSeats))]
        public void SuperAdminOnlyEndpoints_RequireTheSuperAdminRole(string methodName)
        {
            var method = typeof(UsersController).GetMethod(methodName)!;
            var attr = method.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(attr);
            Assert.Equal(RoleNames.SuperAdmin, attr!.Roles);
        }

        [Theory]
        [InlineData(typeof(AppSettingsController))]
        [InlineData(typeof(AuditLogsController))]
        public void SettingsAndAuditLog_AreSuperAdminOnly(Type controllerType)
        {
            var attr = controllerType.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(attr);
            Assert.Equal(RoleNames.SuperAdmin, attr!.Roles);
        }

        [Fact]
        public void UsersController_IsAtLeastAdminOnly()
        {
            var attr = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(attr);
            Assert.Equal(RoleNames.Admin, attr!.Roles);
        }

        [Fact]
        public void User_PasswordHash_IsNeverSerialized()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new User { Id = 1, PasswordHash = "secret-hash" });

            Assert.DoesNotContain("secret-hash", json);
            Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
