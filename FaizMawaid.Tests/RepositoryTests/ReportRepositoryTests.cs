using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class ReportRepositoryTests
    {
        [Fact]
        public async Task GetDailyThaaliCountAsync_CountsTheApprovedActiveFamilyUnderItsSize()
        {
            var factory = TestConnectionFactory.Create();
            var reportRepository = new ReportRepository(factory);
            var familyRepository = new FamilyRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            var adminUserId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            await familyRepository.ApproveAsync(registration.FamilyId, adminUserId);
            var testDate = TestDataHelper.RandomFutureDate();
            try
            {
                var report = await reportRepository.GetDailyThaaliCountAsync(testDate);

                var line = report.ByThaaliSize.Single(x => x.ThaaliSizeId == thaaliSizeId);
                Assert.Equal(1, line.Count);
                Assert.True(report.TotalThaalis >= 1);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteUserAsync(factory, adminUserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }

        [Fact]
        public async Task GetMonthlyThaaliDistributedByAreaAsync_CountsAnApprovedFamilyOnAServingDayUnderItsArea()
        {
            var factory = TestConnectionFactory.Create();
            var reportRepository = new ReportRepository(factory);
            var familyRepository = new FamilyRepository(factory);
            var areaId = await TestDataHelper.CreateAreaAsync(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId, areaId);
            var adminUserId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            await familyRepository.ApproveAsync(registration.FamilyId, adminUserId);

            var testDate = NextMonday(TestDataHelper.RandomFutureDate());
            var fromDate = new DateOnly(testDate.Year, testDate.Month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            try
            {
                var report = await reportRepository.GetMonthlyThaaliDistributedByAreaAsync(fromDate, toDate);

                Assert.Contains(report.Areas, a => a.AreaId == areaId);
                var month = report.Months.Single(m => m.Year == testDate.Year && m.Month == testDate.Month);
                var key = ReportAreaKey.For(areaId);
                Assert.True(month.CountsByArea.ContainsKey(key));
                Assert.True(month.CountsByArea[key] >= 1);
                Assert.True(month.Total >= 1);
                Assert.True(report.GrandTotal >= 1);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteUserAsync(factory, adminUserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
                await TestDataHelper.DeleteAreaAsync(factory, areaId);
            }
        }

        [Fact]
        public async Task GetMonthlyThaaliCancelledByAreaAsync_CountsAnActiveCancellationUnderItsFamilysArea()
        {
            var factory = TestConnectionFactory.Create();
            var reportRepository = new ReportRepository(factory);
            var familyRepository = new FamilyRepository(factory);
            var cancellationRepository = new ThaaliCancellationRepository(factory);
            var areaId = await TestDataHelper.CreateAreaAsync(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId, areaId);
            var adminUserId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            await familyRepository.ApproveAsync(registration.FamilyId, adminUserId);

            var testDate = NextMonday(TestDataHelper.RandomFutureDate());
            var fromDate = new DateOnly(testDate.Year, testDate.Month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            await cancellationRepository.CreateAsync(new CreateThaaliCancellationRequest
            {
                FamilyId = registration.FamilyId,
                StartDate = testDate,
                EndDate = testDate,
                Reason = "Unit test",
                CreatedByUserId = registration.UserId
            });

            try
            {
                var report = await reportRepository.GetMonthlyThaaliCancelledByAreaAsync(fromDate, toDate);

                var month = report.Months.Single(m => m.Year == testDate.Year && m.Month == testDate.Month);
                var key = ReportAreaKey.For(areaId);
                Assert.True(month.CountsByArea.ContainsKey(key));
                Assert.Equal(1, month.CountsByArea[key]);
                Assert.True(month.Total >= 1);
                Assert.True(report.GrandTotal >= 1);
            }
            finally
            {
                // DeleteFamilyAsync also removes any ThaaliCancellations rows for this family.
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteUserAsync(factory, adminUserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
                await TestDataHelper.DeleteAreaAsync(factory, areaId);
            }
        }

        private static DateOnly NextMonday(DateOnly date)
        {
            while (date.DayOfWeek != DayOfWeek.Monday)
            {
                date = date.AddDays(1);
            }
            return date;
        }
    }
}
