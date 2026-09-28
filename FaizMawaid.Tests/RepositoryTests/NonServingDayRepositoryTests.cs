using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class NonServingDayRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_ThenIsNonServingDayAsync_ReturnsTrue()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new NonServingDayRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory);
            var date = TestDataHelper.RandomFutureDate();
            uint id = 0;
            try
            {
                id = await repository.CreateAsync(new CreateNonServingDayRequest
                {
                    TheDate = date,
                    Reason = "Unit test holiday",
                    CreatedByUserId = userId
                });

                var isNonServing = await repository.IsNonServingDayAsync(date);

                Assert.True(isNonServing);
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
        public async Task DeleteAsync_RemovesTheNonServingDay()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new NonServingDayRepository(factory);
            var userId = await TestDataHelper.CreateUserAsync(factory);
            var date = TestDataHelper.RandomFutureDate();
            var id = await repository.CreateAsync(new CreateNonServingDayRequest { TheDate = date, CreatedByUserId = userId });
            try
            {
                var deleted = await repository.DeleteAsync(id);

                Assert.True(deleted);
                Assert.False(await repository.IsNonServingDayAsync(date));
            }
            finally
            {
                await TestDataHelper.DeleteUserAsync(factory, userId);
            }
        }
    }
}
