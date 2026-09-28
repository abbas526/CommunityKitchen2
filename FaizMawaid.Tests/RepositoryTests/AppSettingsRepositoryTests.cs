using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    /// <summary>AppSettings is a single global row -- this test restores the original value afterwards so it doesn't leave your real app configuration changed.</summary>
    public class AppSettingsRepositoryTests
    {
        [Fact]
        public async Task UpdateAsync_ThenGetAsync_ReflectsTheNewMealVisibilityDays()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new AppSettingsRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory);
            var original = await repository.GetAsync();
            try
            {
                var updated = await repository.UpdateAsync(new UpdateAppSettingsRequest
                {
                    MealVisibilityDays = 10,
                    UpdatedByUserId = userId
                });

                Assert.True(updated);
                var fetched = await repository.GetAsync();
                Assert.Equal(10u, fetched.MealVisibilityDays);
            }
            finally
            {
                await repository.UpdateAsync(new UpdateAppSettingsRequest
                {
                    MealVisibilityDays = original.MealVisibilityDays,
                    UpdatedByUserId = original.UpdatedByUserId
                });
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }
    }
}
