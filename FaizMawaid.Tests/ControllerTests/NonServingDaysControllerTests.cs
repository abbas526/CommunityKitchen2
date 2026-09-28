using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class NonServingDaysControllerTests
    {
        private static DateOnly NextNonSunday(int daysAhead = 30)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
            while (date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
            }
            return date;
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithDays()
        {
            var repo = new Mock<INonServingDayRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var days = new List<NonServingDay> { new() { Id = 1, TheDate = NextNonSunday() } };
            repo.Setup(r => r.GetAllAsync(null)).ReturnsAsync(days);
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object);

            var result = await controller.GetAll(null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(days, okResult.Value);
        }

        [Fact]
        public async Task Create_ReturnsCreatedAtActionForNonSundayDate()
        {
            var repo = new Mock<INonServingDayRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var request = new CreateNonServingDayRequest
            {
                TheDate = NextNonSunday(),
                Reason = "Festival",
                CreatedByUserId = 1
            };
            repo.Setup(r => r.CreateAsync(request)).ReturnsAsync(99u);
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object);

            var result = await controller.Create(request);

            Assert.IsType<CreatedAtActionResult>(result);
            auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Once);
        }

        [Fact]
        public async Task Delete_ReturnsNoContentWhenDeleted()
        {
            var repo = new Mock<INonServingDayRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            repo.Setup(r => r.DeleteAsync(4u)).ReturnsAsync(true);
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object);

            var result = await controller.Delete(4u);

            Assert.IsType<NoContentResult>(result);
        }
    }
}
