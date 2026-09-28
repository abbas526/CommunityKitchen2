using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class MealPlanRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_ThenGetByDateAsync_ReturnsTheCreatedMealPlan()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new MealPlanRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory);
            var date = TestDataHelper.RandomFutureDate();
            ulong id = 0;
            try
            {
                id = await repository.CreateAsync(new CreateMealPlanRequest
                {
                    MealDate = date,
                    MealDescription = "Rice, dal, and mixed vegetable curry.",
                    CreatedByUserId = userId
                });

                var fetched = await repository.GetByDateAsync(date);

                Assert.NotNull(fetched);
                Assert.Equal("Rice, dal, and mixed vegetable curry.", fetched!.MealDescription);
            }
            finally
            {
                if (id != 0)
                {
                    await repository.DeleteAsync(id);
                }
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }

        [Fact]
        public async Task UpdateAsync_ChangesMealDescription()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new MealPlanRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory);
            var date = TestDataHelper.RandomFutureDate();
            var id = await repository.CreateAsync(new CreateMealPlanRequest
            {
                MealDate = date,
                MealDescription = "Original menu text.",
                CreatedByUserId = userId
            });
            try
            {
                var updated = await repository.UpdateAsync(id, new UpdateMealPlanRequest
                {
                    MealDescription = "Updated menu text.",
                    UpdatedByUserId = userId
                });

                Assert.True(updated);
                var fetched = await repository.GetByIdAsync(id);
                Assert.Equal("Updated menu text.", fetched!.MealDescription);
                Assert.Equal(userId, fetched.UpdatedByUserId);
            }
            finally
            {
                await repository.DeleteAsync(id);
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }

        [Fact]
        public async Task GetRangeAsync_IncludesTheCreatedMealPlan()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new MealPlanRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory);
            var date = TestDataHelper.RandomFutureDate();
            var id = await repository.CreateAsync(new CreateMealPlanRequest
            {
                MealDate = date,
                MealDescription = "Range test meal.",
                CreatedByUserId = userId
            });
            try
            {
                var range = await repository.GetRangeAsync(date.AddDays(-1), date.AddDays(1));

                Assert.Contains(range, m => m.Id == id);
            }
            finally
            {
                await repository.DeleteAsync(id);
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }
    }
}
