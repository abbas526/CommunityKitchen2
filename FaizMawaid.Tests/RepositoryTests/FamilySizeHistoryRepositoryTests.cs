using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class FamilySizeHistoryRepositoryTests
    {
        [Fact]
        public async Task GetSizeOnDateAsync_ReturnsTheOpeningHistoryRowForToday()
        {
            var factory = TestConnectionFactory.Create();
            var historyRepository = new FamilySizeHistoryRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                var sizeOnDate = await historyRepository.GetSizeOnDateAsync(registration.FamilyId, today);

                Assert.NotNull(sizeOnDate);
                Assert.Equal(thaaliSizeId, sizeOnDate!.ThaaliSizeId);
                Assert.Null(sizeOnDate.EffectiveToDate);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }

        [Fact]
        public async Task GetByFamilyIdAsync_ReturnsTheOpeningHistoryRow()
        {
            var factory = TestConnectionFactory.Create();
            var historyRepository = new FamilySizeHistoryRepository(factory);
            var thaaliSizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, thaaliSizeId);
            try
            {
                var history = await historyRepository.GetByFamilyIdAsync(registration.FamilyId);

                Assert.Single(history);
                Assert.Equal(thaaliSizeId, history.First().ThaaliSizeId);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, thaaliSizeId);
            }
        }
    }
}
