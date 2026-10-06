using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Utils;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class NonServingDaysControllerTests
    {
        /// <summary>A date that passes both the Sunday and Ramadan serving-day rules -- so tests
        /// that are just exercising something else don't flake if "30 days from today" happens to
        /// land in Ramadan for whatever year the tests run in.</summary>
        private static DateOnly NextNonSunday(int daysAhead = 30)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
            while (date.DayOfWeek == DayOfWeek.Sunday || MisriCalendar.IsRamadan(date))
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
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

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
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.Create(request);

            Assert.IsType<CreatedAtActionResult>(result);
            auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Once);
        }

        [Fact]
        public async Task Create_RejectsDateWithinRamadan()
        {
            var repo = new Mock<INonServingDayRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var request = new CreateNonServingDayRequest
            {
                TheDate = new DateOnly(2027, 2, 15),
                Reason = "Festival",
                CreatedByUserId = 1
            };
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.Create(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Ramadan", badRequest.Value!.ToString());
            repo.Verify(r => r.CreateAsync(It.IsAny<CreateNonServingDayRequest>()), Times.Never);
        }

        [Fact]
        public async Task Delete_ReturnsNoContentWhenDeleted()
        {
            var repo = new Mock<INonServingDayRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            repo.Setup(r => r.DeleteAsync(4u)).ReturnsAsync(true);
            var controller = new NonServingDaysController(repo.Object, auditRepo.Object, new Mock<IMealPlanRepository>().Object);

            var result = await controller.Delete(4u);

            Assert.IsType<NoContentResult>(result);
        }
    }
}
