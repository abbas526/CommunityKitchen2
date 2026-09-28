using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class MealPlanTemplateRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_ThenGetByIdAsync_ReturnsTheCreatedTemplate()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new MealPlanTemplateRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            var name = "UT_" + Guid.NewGuid().ToString("N")[..10];
            ushort id = 0;
            try
            {
                id = await repository.CreateAsync(new CreateMealPlanTemplateRequest
                {
                    Name = name,
                    MealDescription = "Rice, dal, mixed vegetable curry, roti",
                    SortOrder = 5,
                    CreatedByUserId = userId
                });

                var fetched = await repository.GetByIdAsync(id);

                Assert.NotNull(fetched);
                Assert.Equal(name, fetched!.Name);
                Assert.Equal("Rice, dal, mixed vegetable curry, roti", fetched.MealDescription);
                Assert.Equal(5, fetched.SortOrder);
            }
            finally
            {
                if (id != 0) { await repository.DeleteAsync(id); }
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }

        [Fact]
        public async Task UpdateAsync_ChangesFieldsAndGetAllAsync_IncludesIt()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new MealPlanTemplateRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            var name = "UT_" + Guid.NewGuid().ToString("N")[..10];
            var id = await repository.CreateAsync(new CreateMealPlanTemplateRequest { Name = name, MealDescription = "Original", SortOrder = 1, CreatedByUserId = userId });
            try
            {
                var newName = name + "_Updated";
                var updated = await repository.UpdateAsync(id, new UpdateMealPlanTemplateRequest { Name = newName, MealDescription = "Updated desc", SortOrder = 2 });

                Assert.True(updated);
                var all = await repository.GetAllAsync();
                Assert.Contains(all, t => t.Id == id && t.Name == newName && t.MealDescription == "Updated desc");
            }
            finally
            {
                await repository.DeleteAsync(id);
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }

        [Fact]
        public async Task DeleteAsync_RemovesTheTemplate()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new MealPlanTemplateRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory, RoleIds.Admin);
            var name = "UT_" + Guid.NewGuid().ToString("N")[..10];
            var id = await repository.CreateAsync(new CreateMealPlanTemplateRequest { Name = name, MealDescription = "Desc", SortOrder = 1, CreatedByUserId = userId });
            try
            {
                var deleted = await repository.DeleteAsync(id);

                Assert.True(deleted);
                Assert.Null(await repository.GetByIdAsync(id));
            }
            finally
            {
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }
    }
}
