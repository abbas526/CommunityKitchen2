using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class FamiliesControllerTests
    {
        private static FamiliesController CreateController(
            Mock<IFamilyRepository> familyRepo,
            Mock<IFamilySizeHistoryRepository>? historyRepo = null,
            Mock<IThaaliSizeRepository>? thaaliSizeRepo = null,
            Mock<IAreaRepository>? areaRepo = null,
            Mock<IUserRepository>? userRepo = null,
            Mock<IAuditLogRepository>? auditLogRepo = null,
            Mock<IPasswordHasherService>? passwordHasher = null)
        {
            historyRepo ??= new Mock<IFamilySizeHistoryRepository>();
            thaaliSizeRepo ??= new Mock<IThaaliSizeRepository>();
            areaRepo ??= new Mock<IAreaRepository>();
            userRepo ??= new Mock<IUserRepository>();
            auditLogRepo ??= new Mock<IAuditLogRepository>();
            passwordHasher ??= new Mock<IPasswordHasherService>();
            passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
            return new FamiliesController(familyRepo.Object, historyRepo.Object, thaaliSizeRepo.Object, areaRepo.Object, userRepo.Object, auditLogRepo.Object, passwordHasher.Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithFamilies()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var families = new List<Family> { new() { Id = 1, RegistrationStatus = RegistrationStatus.Approved } };
            familyRepo.Setup(r => r.GetAllAsync(null)).ReturnsAsync(families);
            var controller = CreateController(familyRepo);

            var result = await controller.GetAll(null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(families, okResult.Value);
        }

        [Fact]
        public async Task GetById_ReturnsOkWhenFamilyExists()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var family = new Family { Id = 4, RegistrationStatus = RegistrationStatus.Approved };
            familyRepo.Setup(r => r.GetByIdAsync(4UL)).ReturnsAsync(family);
            var controller = CreateController(familyRepo);

            var result = await controller.GetById(4);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(family, okResult.Value);
        }

        [Fact]
        public async Task Register_ReturnsCreatedAtActionWhenThaaliSizeExists()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var thaaliSizeRepo = new Mock<IThaaliSizeRepository>();
            var userRepo = new Mock<IUserRepository>();
            var auditLogRepo = new Mock<IAuditLogRepository>();
            var request = new RegisterFamilyRequest
            {
                Email = "head@unittest.local",
                SabilNumber = "SB-001",
                Password = "TestPass123",
                PasswordHash = "hash",
                FullName = "Test Head",
                ThaaliSizeId = 1
            };
            thaaliSizeRepo.Setup(r => r.GetByIdAsync((byte)1)).ReturnsAsync(new ThaaliSize { Id = 1, Name = "Medium", SortOrder = 2 });
            // Left unconfigured deliberately: a loose Mock<IUserRepository> returns null from
            // GetByEmailAsync/GetBySabilNumberAsync by default, i.e. "no existing duplicate".
            var response = new RegisterFamilyResponse { UserId = 10, FamilyId = 20 };
            familyRepo.Setup(r => r.RegisterAsync(request)).ReturnsAsync(response);
            var controller = CreateController(familyRepo, thaaliSizeRepo: thaaliSizeRepo, userRepo: userRepo, auditLogRepo: auditLogRepo);

            var result = await controller.Register(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(response, createdResult.Value);
            auditLogRepo.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "FamilyRegistered")), Times.Once);
        }

        [Fact]
        public async Task Register_ReturnsBadRequestWhenSabilNumberIsMissing()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var controller = CreateController(familyRepo);
            var request = new RegisterFamilyRequest { Email = "head@unittest.local", SabilNumber = "  ", ThaaliSizeId = 1 };

            var result = await controller.Register(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Register_ReturnsBadRequestWhenPasswordIsTooShort()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var controller = CreateController(familyRepo);
            var request = new RegisterFamilyRequest { Email = "head@unittest.local", SabilNumber = "SB-002", Password = "short", ThaaliSizeId = 1 };

            var result = await controller.Register(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Register_ReturnsConflictWhenSabilNumberAlreadyRegistered()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var thaaliSizeRepo = new Mock<IThaaliSizeRepository>();
            var userRepo = new Mock<IUserRepository>();
            thaaliSizeRepo.Setup(r => r.GetByIdAsync((byte)1)).ReturnsAsync(new ThaaliSize { Id = 1, Name = "Medium", SortOrder = 2 });
            userRepo.Setup(r => r.GetBySabilNumberAsync("SB-999")).ReturnsAsync(new User { Id = 5, SabilNumber = "SB-999" });
            var controller = CreateController(familyRepo, thaaliSizeRepo: thaaliSizeRepo, userRepo: userRepo);
            var request = new RegisterFamilyRequest { Email = "another@unittest.local", SabilNumber = "SB-999", Password = "TestPass123", ThaaliSizeId = 1 };

            var result = await controller.Register(request);

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task Approve_ReturnsNoContentAndLogsAudit()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var auditLogRepo = new Mock<IAuditLogRepository>();
            familyRepo.Setup(r => r.ApproveAsync(5UL, 9UL)).ReturnsAsync(true);
            var controller = CreateController(familyRepo, auditLogRepo: auditLogRepo);

            var result = await controller.Approve(5, new ApproveFamilyRequest { ApprovedByAdminUserId = 9 });

            Assert.IsType<NoContentResult>(result);
            auditLogRepo.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "FamilyApproved")), Times.Once);
        }

        [Fact]
        public async Task Reject_ReturnsNoContentAndLogsAudit()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var auditLogRepo = new Mock<IAuditLogRepository>();
            familyRepo.Setup(r => r.RejectAsync(6UL, 9UL)).ReturnsAsync(true);
            var controller = CreateController(familyRepo, auditLogRepo: auditLogRepo);

            var result = await controller.Reject(6, new RejectFamilyRequest { RejectedByAdminUserId = 9 });

            Assert.IsType<NoContentResult>(result);
            auditLogRepo.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "FamilyRejected")), Times.Once);
        }

        [Fact]
        public async Task ChangeThaaliSize_ReturnsNoContentWhenSizeExistsAndUpdateSucceeds()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var thaaliSizeRepo = new Mock<IThaaliSizeRepository>();
            var auditLogRepo = new Mock<IAuditLogRepository>();
            var request = new ChangeThaaliSizeRequest { NewThaaliSizeId = 2, ChangedByUserId = 1, EffectiveFromDate = DateOnly.FromDateTime(DateTime.UtcNow) };
            thaaliSizeRepo.Setup(r => r.GetByIdAsync((byte)2)).ReturnsAsync(new ThaaliSize { Id = 2, Name = "Large", SortOrder = 3 });
            familyRepo.Setup(r => r.ChangeThaaliSizeAsync(8UL, request)).ReturnsAsync(true);
            var controller = CreateController(familyRepo, thaaliSizeRepo: thaaliSizeRepo, auditLogRepo: auditLogRepo);

            var result = await controller.ChangeThaaliSize(8, request);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task GetSizeHistory_ReturnsOkWithHistory()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var historyRepo = new Mock<IFamilySizeHistoryRepository>();
            var history = new List<FamilySizeHistory> { new() { Id = 1, FamilyId = 8, ThaaliSizeId = 2 } };
            historyRepo.Setup(r => r.GetByFamilyIdAsync(8UL)).ReturnsAsync(history);
            var controller = CreateController(familyRepo, historyRepo: historyRepo);

            var result = await controller.GetSizeHistory(8);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(history, okResult.Value);
        }

        [Fact]
        public async Task Activate_ReturnsNoContentWhenUpdated()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            familyRepo.Setup(r => r.SetActiveAsync(3UL, true)).ReturnsAsync(true);
            var controller = CreateController(familyRepo);

            var result = await controller.Activate(3);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task GetSubFamilies_ReturnsOkWhenParentExists()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var parent = new Family { Id = 1, RegistrationStatus = RegistrationStatus.Approved };
            var subFamilies = new List<Family> { new() { Id = 2, ParentFamilyId = 1, SubFamilyLabel = "Son's family - Bldg B" } };
            familyRepo.Setup(r => r.GetByIdAsync(1UL)).ReturnsAsync(parent);
            familyRepo.Setup(r => r.GetSubFamiliesAsync(1UL)).ReturnsAsync(subFamilies);
            var controller = CreateController(familyRepo);

            var result = await controller.GetSubFamilies(1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(subFamilies, okResult.Value);
        }

        [Fact]
        public async Task GetSubFamilies_ReturnsNotFoundWhenParentDoesNotExist()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            familyRepo.Setup(r => r.GetByIdAsync(99UL)).ReturnsAsync((Family?)null);
            var controller = CreateController(familyRepo);

            var result = await controller.GetSubFamilies(99);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task CreateSubFamily_ReturnsCreatedAtActionWhenValid()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var thaaliSizeRepo = new Mock<IThaaliSizeRepository>();
            var userRepo = new Mock<IUserRepository>();
            var auditLogRepo = new Mock<IAuditLogRepository>();
            var parent = new Family { Id = 1, RegistrationStatus = RegistrationStatus.Approved };
            var admin = new User { Id = 9, RoleId = RoleIds.Admin };
            var request = new CreateSubFamilyRequest { SubFamilyLabel = "Son's family - Bldg B", ThaaliSizeId = 1, CreatedByAdminUserId = 9 };
            var created = new Family { Id = 2, ParentFamilyId = 1, SubFamilyLabel = request.SubFamilyLabel };
            familyRepo.Setup(r => r.GetByIdAsync(1UL)).ReturnsAsync(parent);
            userRepo.Setup(r => r.GetByIdAsync(9UL)).ReturnsAsync(admin);
            thaaliSizeRepo.Setup(r => r.GetByIdAsync((byte)1)).ReturnsAsync(new ThaaliSize { Id = 1, Name = "Medium", SortOrder = 2 });
            familyRepo.Setup(r => r.CreateSubFamilyAsync(1UL, request)).ReturnsAsync(2UL);
            familyRepo.Setup(r => r.GetByIdAsync(2UL)).ReturnsAsync(created);
            var controller = CreateController(familyRepo, thaaliSizeRepo: thaaliSizeRepo, userRepo: userRepo, auditLogRepo: auditLogRepo);

            var result = await controller.CreateSubFamily(1, request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
            auditLogRepo.Verify(r => r.AddAsync(It.Is<AuditLog>(a => a.Action == "SubFamilyCreated")), Times.Once);
        }

        [Fact]
        public async Task CreateSubFamily_ReturnsBadRequestWhenCreatorIsNotAnAdmin()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var userRepo = new Mock<IUserRepository>();
            var parent = new Family { Id = 1, RegistrationStatus = RegistrationStatus.Approved };
            var nonAdmin = new User { Id = 7, RoleId = RoleIds.FamilyHead };
            familyRepo.Setup(r => r.GetByIdAsync(1UL)).ReturnsAsync(parent);
            userRepo.Setup(r => r.GetByIdAsync(7UL)).ReturnsAsync(nonAdmin);
            var controller = CreateController(familyRepo, userRepo: userRepo);
            var request = new CreateSubFamilyRequest { SubFamilyLabel = "Son's family", ThaaliSizeId = 1, CreatedByAdminUserId = 7 };

            var result = await controller.CreateSubFamily(1, request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateSubFamily_ReturnsBadRequestWhenParentIsItselfASubFamily()
        {
            var familyRepo = new Mock<IFamilyRepository>();
            var grandparentedSubFamily = new Family { Id = 2, ParentFamilyId = 1, RegistrationStatus = RegistrationStatus.Approved };
            familyRepo.Setup(r => r.GetByIdAsync(2UL)).ReturnsAsync(grandparentedSubFamily);
            var controller = CreateController(familyRepo);
            var request = new CreateSubFamilyRequest { SubFamilyLabel = "Grandchild household", ThaaliSizeId = 1, CreatedByAdminUserId = 9 };

            var result = await controller.CreateSubFamily(2, request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
