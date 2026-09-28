using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class AppSettingsControllerTests
    {
        [Fact]
        public async Task Get_ReturnsOkWithSettings()
        {
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var settings = new AppSetting { Id = 1, MealVisibilityDays = 7 };
            settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(settings);
            var controller = new AppSettingsController(settingsRepo.Object, auditRepo.Object);

            var result = await controller.Get();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(settings, okResult.Value);
        }

        [Fact]
        public async Task Update_ReturnsNoContentAndLogsAuditForValidValue()
        {
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var request = new UpdateAppSettingsRequest { MealVisibilityDays = 10, UpdatedByUserId = 1 };
            settingsRepo.Setup(r => r.UpdateAsync(request)).ReturnsAsync(true);
            var controller = new AppSettingsController(settingsRepo.Object, auditRepo.Object);

            var result = await controller.Update(request);

            Assert.IsType<NoContentResult>(result);
            auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Once);
        }
    }
}
