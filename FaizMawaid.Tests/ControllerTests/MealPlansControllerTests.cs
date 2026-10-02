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
    public class MealPlansControllerTests
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

        /// <summary>A date guaranteed to fall within Ramadan, for tests of the Ramadan rejection rule.</summary>
        private static DateOnly ADateInRamadan() => new DateOnly(2027, 2, 15);

        [Fact]
        public async Task Create_ReturnsCreatedAtActionForValidServingDate()
        {
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var nonServingRepo = new Mock<INonServingDayRepository>();
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var date = NextNonSunday();
            var request = new CreateMealPlanRequest { MealDate = date, MealDescription = "Rice and dal.", CreatedByUserId = 1 };
            nonServingRepo.Setup(r => r.IsNonServingDayAsync(date)).ReturnsAsync(false);
            mealPlanRepo.Setup(r => r.GetByDateAsync(date)).ReturnsAsync((MealPlan?)null);
            mealPlanRepo.Setup(r => r.CreateAsync(request)).ReturnsAsync(11UL);
            var created = new MealPlan { Id = 11, MealDate = date, MealDescription = request.MealDescription };
            mealPlanRepo.Setup(r => r.GetByIdAsync(11UL)).ReturnsAsync(created);
            var controller = new MealPlansController(mealPlanRepo.Object, nonServingRepo.Object, settingsRepo.Object, auditRepo.Object);

            var result = await controller.Create(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
            auditRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Once);
        }

        [Fact]
        public async Task Create_RejectsDateWithinRamadan()
        {
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var nonServingRepo = new Mock<INonServingDayRepository>();
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var date = ADateInRamadan();
            var request = new CreateMealPlanRequest { MealDate = date, MealDescription = "Rice and dal.", CreatedByUserId = 1 };
            var controller = new MealPlansController(mealPlanRepo.Object, nonServingRepo.Object, settingsRepo.Object, auditRepo.Object);

            var result = await controller.Create(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Contains("Ramadan", badRequest.Value!.ToString());
            mealPlanRepo.Verify(r => r.CreateAsync(It.IsAny<CreateMealPlanRequest>()), Times.Never);
        }

        [Fact]
        public async Task GetUpcoming_ReturnsMealsWithinConfiguredVisibilityWindow()
        {
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var nonServingRepo = new Mock<INonServingDayRepository>();
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            settingsRepo.Setup(r => r.GetAsync()).ReturnsAsync(new AppSetting { Id = 1, MealVisibilityDays = 7 });
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var meals = new List<MealPlan> { new() { Id = 1, MealDate = today, MealDescription = "Today's meal." } };
            mealPlanRepo.Setup(r => r.GetRangeAsync(today, today.AddDays(6))).ReturnsAsync(meals);
            var controller = new MealPlansController(mealPlanRepo.Object, nonServingRepo.Object, settingsRepo.Object, auditRepo.Object);

            var result = await controller.GetUpcoming();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(meals, okResult.Value);
        }

        [Fact]
        public async Task GetRange_ReturnsOkWhenRangeValid()
        {
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var nonServingRepo = new Mock<INonServingDayRepository>();
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var from = DateOnly.FromDateTime(DateTime.UtcNow);
            var to = from.AddDays(10);
            var meals = new List<MealPlan> { new() { Id = 1, MealDate = from } };
            mealPlanRepo.Setup(r => r.GetRangeAsync(from, to)).ReturnsAsync(meals);
            var controller = new MealPlansController(mealPlanRepo.Object, nonServingRepo.Object, settingsRepo.Object, auditRepo.Object);

            var result = await controller.GetRange(from, to);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(meals, okResult.Value);
        }

        [Fact]
        public async Task Update_ReturnsNoContentWhenUpdated()
        {
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var nonServingRepo = new Mock<INonServingDayRepository>();
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            var request = new UpdateMealPlanRequest { MealDescription = "Updated.", UpdatedByUserId = 1 };
            mealPlanRepo.Setup(r => r.UpdateAsync(9UL, request)).ReturnsAsync(true);
            var controller = new MealPlansController(mealPlanRepo.Object, nonServingRepo.Object, settingsRepo.Object, auditRepo.Object);

            var result = await controller.Update(9, request);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_ReturnsNoContentWhenDeleted()
        {
            var mealPlanRepo = new Mock<IMealPlanRepository>();
            var nonServingRepo = new Mock<INonServingDayRepository>();
            var settingsRepo = new Mock<IAppSettingsRepository>();
            var auditRepo = new Mock<IAuditLogRepository>();
            mealPlanRepo.Setup(r => r.DeleteAsync(6UL)).ReturnsAsync(true);
            var controller = new MealPlansController(mealPlanRepo.Object, nonServingRepo.Object, settingsRepo.Object, auditRepo.Object);

            var result = await controller.Delete(6);

            Assert.IsType<NoContentResult>(result);
        }
    }
}
