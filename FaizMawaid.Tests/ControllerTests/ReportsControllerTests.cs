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

        [Fact]
        public async Task GetMonthlyThaaliDistributed_ReturnsOkWithReport()
        {
            var reportRepo = new Mock<IReportRepository>();
            var from = new DateOnly(2026, 1, 1);
            var to = new DateOnly(2026, 3, 31);
            var report = new MonthlyAreaReportResponse
            {
                FromDate = from,
                ToDate = to,
                Areas = new List<ReportAreaColumn> { new() { AreaId = 1, AreaName = "Tower A" } },
                Months = new List<MonthlyAreaCountRow>(),
                GrandTotal = 0
            };
            reportRepo.Setup(r => r.GetMonthlyThaaliDistributedByAreaAsync(from, to)).ReturnsAsync(report);
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetMonthlyThaaliDistributed(from, to);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(report, okResult.Value);
        }

        [Fact]
        public async Task GetMonthlyThaaliDistributed_DefaultsToJanuaryOfCurrentYearThroughToday_WhenDatesOmitted()
        {
            var reportRepo = new Mock<IReportRepository>();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expectedFrom = new DateOnly(today.Year, 1, 1);
            reportRepo.Setup(r => r.GetMonthlyThaaliDistributedByAreaAsync(expectedFrom, today))
                .ReturnsAsync(new MonthlyAreaReportResponse { FromDate = expectedFrom, ToDate = today });
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetMonthlyThaaliDistributed(null, null);

            Assert.IsType<OkObjectResult>(result.Result);
            reportRepo.Verify(r => r.GetMonthlyThaaliDistributedByAreaAsync(expectedFrom, today), Times.Once);
        }

        [Fact]
        public async Task GetMonthlyThaaliDistributed_RejectsEndDateBeforeStartDate()
        {
            var reportRepo = new Mock<IReportRepository>();
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetMonthlyThaaliDistributed(new DateOnly(2026, 3, 1), new DateOnly(2026, 1, 1));

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetMonthlyThaaliCancelled_ReturnsOkWithReport()
        {
            var reportRepo = new Mock<IReportRepository>();
            var from = new DateOnly(2026, 1, 1);
            var to = new DateOnly(2026, 3, 31);
            var report = new MonthlyAreaReportResponse { FromDate = from, ToDate = to };
            reportRepo.Setup(r => r.GetMonthlyThaaliCancelledByAreaAsync(from, to)).ReturnsAsync(report);
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetMonthlyThaaliCancelled(from, to);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(report, okResult.Value);
        }

        [Fact]
        public async Task GetMonthlyThaaliCancelled_DefaultsToJanuaryOfCurrentYearThroughToday_WhenDatesOmitted()
        {
            var reportRepo = new Mock<IReportRepository>();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expectedFrom = new DateOnly(today.Year, 1, 1);
            reportRepo.Setup(r => r.GetMonthlyThaaliCancelledByAreaAsync(expectedFrom, today))
                .ReturnsAsync(new MonthlyAreaReportResponse { FromDate = expectedFrom, ToDate = today });
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetMonthlyThaaliCancelled(null, null);

            Assert.IsType<OkObjectResult>(result.Result);
            reportRepo.Verify(r => r.GetMonthlyThaaliCancelledByAreaAsync(expectedFrom, today), Times.Once);
        }

        [Fact]
        public async Task GetMonthlyThaaliCancelled_RejectsRangeOverThreeYears()
        {
            var reportRepo = new Mock<IReportRepository>();
            var controller = new ReportsController(reportRepo.Object);

            var result = await controller.GetMonthlyThaaliCancelled(new DateOnly(2020, 1, 1), new DateOnly(2026, 1, 1));

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
