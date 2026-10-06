using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class ThaaliCancellationsControllerTests
    {
        [Fact]
        public async Task GetById_ReturnsOkWhenFound()
        {
            var cancellationRepo = new Mock<IThaaliCancellationRepository>();
            var familyRepo = new Mock<IFamilyRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var cancellation = new ThaaliCancellation { Id = 1, FamilyId = 2, Status = ThaaliCancellationStatus.Active };
            cancellationRepo.Setup(r => r.GetByIdAsync(1UL)).ReturnsAsync(cancellation);
            var controller = new ThaaliCancellationsController(cancellationRepo.Object, familyRepo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.GetById(1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(cancellation, okResult.Value);
        }

        [Fact]
        public async Task GetByFamily_ReturnsOkWithCancellations()
        {
            var cancellationRepo = new Mock<IThaaliCancellationRepository>();
            var familyRepo = new Mock<IFamilyRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var list = new List<ThaaliCancellation> { new() { Id = 1, FamilyId = 3 } };
            cancellationRepo.Setup(r => r.GetByFamilyIdAsync(3UL)).ReturnsAsync(list);
            var controller = new ThaaliCancellationsController(cancellationRepo.Object, familyRepo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.GetByFamily(3);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(list, okResult.Value);
        }

        [Fact]
        public async Task Create_ReturnsCreatedAtActionForValidApprovedFamily()
        {
            var cancellationRepo = new Mock<IThaaliCancellationRepository>();
            var familyRepo = new Mock<IFamilyRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var request = new CreateThaaliCancellationRequest
            {
                FamilyId = 3,
                StartDate = start,
                EndDate = start,
                CreatedByUserId = 7
            };
            familyRepo.Setup(r => r.GetByIdAsync(3UL)).ReturnsAsync(new Family
            {
                Id = 3,
                RegistrationStatus = RegistrationStatus.Approved,
                IsActive = true
            });
            cancellationRepo.Setup(r => r.CreateAsync(request)).ReturnsAsync(55UL);
            var created = new ThaaliCancellation { Id = 55, FamilyId = 3, Status = ThaaliCancellationStatus.Active };
            cancellationRepo.Setup(r => r.GetByIdAsync(55UL)).ReturnsAsync(created);
            var controller = new ThaaliCancellationsController(cancellationRepo.Object, familyRepo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.Create(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
            auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Once);
        }

        [Fact]
        public async Task Reinstate_ReturnsNoContentWhenReinstated()
        {
            var cancellationRepo = new Mock<IThaaliCancellationRepository>();
            var familyRepo = new Mock<IFamilyRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            cancellationRepo.Setup(r => r.ReinstateAsync(10UL, 4UL)).ReturnsAsync(true);
            var controller = new ThaaliCancellationsController(cancellationRepo.Object, familyRepo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.Reinstate(10, new ReinstateThaaliCancellationRequest { ReinstatedByUserId = 4 });

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Create_RejectsAFamilyThatOnlyTakesSpecialDaysWhenTheRangeHasNoSpecialDay()
        {
            var cancellationRepo = new Mock<IThaaliCancellationRepository>();
            var familyRepo = new Mock<IFamilyRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var request = new CreateThaaliCancellationRequest { FamilyId = 3, StartDate = start, EndDate = start.AddDays(2), CreatedByUserId = 7 };
            familyRepo.Setup(r => r.GetByIdAsync(3UL)).ReturnsAsync(new Family { Id = 3, RegistrationStatus = RegistrationStatus.Approved, IsActive = true, TakesRegularMeal = false });
            mealPlanRepo.Setup(r => r.GetSpecialDaysAsync(start, start.AddDays(2))).ReturnsAsync(new List<MealPlan>());
            var controller = new ThaaliCancellationsController(cancellationRepo.Object, familyRepo.Object, auditRepo.Object, mealPlanRepo.Object);

            var result = await controller.Create(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            cancellationRepo.Verify(r => r.CreateAsync(It.IsAny<CreateThaaliCancellationRequest>()), Times.Never);
        }

        [Fact]
        public async Task Create_AllowsAFamilyThatOnlyTakesSpecialDaysWhenTheRangeContainsOne()
        {
            var cancellationRepo = new Mock<IThaaliCancellationRepository>();
            var familyRepo = new Mock<IFamilyRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var request = new CreateThaaliCancellationRequest { FamilyId = 3, StartDate = start, EndDate = start, CreatedByUserId = 7 };
            familyRepo.Setup(r => r.GetByIdAsync(3UL)).ReturnsAsync(new Family { Id = 3, RegistrationStatus = RegistrationStatus.Approved, IsActive = true, TakesRegularMeal = false });
            mealPlanRepo.Setup(r => r.GetSpecialDaysAsync(start, start)).ReturnsAsync(new List<MealPlan> { new() { Id = 1, MealDate = start, IsSpecialDay = true } });
            cancellationRepo.Setup(r => r.CreateAsync(request)).ReturnsAsync(60UL);
            cancellationRepo.Setup(r => r.GetByIdAsync(60UL)).ReturnsAsync(new ThaaliCancellation { Id = 60, FamilyId = 3 });
            var controller = new ThaaliCancellationsController(cancellationRepo.Object, familyRepo.Object, auditRepo.Object, mealPlanRepo.Object);

            var result = await controller.Create(request);

            Assert.IsType<CreatedAtActionResult>(result.Result);
        }
    }
}
