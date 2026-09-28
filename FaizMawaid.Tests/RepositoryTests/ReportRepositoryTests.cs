using FaizMawaid.Models;
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
    }
}
