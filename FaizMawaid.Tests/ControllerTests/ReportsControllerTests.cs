using FaizMawaid.Controllers;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class ReportsControllerTests
    {
        [Fact]
        public async Task GetDailyThaaliCount_ReturnsOkWithReport()
        {
            var reportRepo = new Mock<IReportRepository>();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var report = new DailyThaaliCountResponse
            {
                Date = date,
                IsServingDay = true,
                TotalThaalis = 5,
                ByThaaliSize = new List<DailyThaaliCountItem> { new() { ThaaliSizeId = 1, ThaaliSizeName = "Medium", Count = 5 } }
            };
            reportRepo.Setup(r => r.GetDailyThaaliCountAsync(date)).ReturnsAsync(report);
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetDailyThaaliCount(date);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(report, okResult.Value);
        }
    }
}
