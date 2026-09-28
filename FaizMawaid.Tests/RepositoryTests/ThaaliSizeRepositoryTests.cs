using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class ThaaliSizeRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_ThenGetByIdAsync_ReturnsTheCreatedThaaliSize()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliSizeRepository(factory);
            var name = "UT_" + Guid.NewGuid().ToString("N")[..10];

            var id = await repository.CreateAsync(new CreateThaaliSizeRequest { Name = name, SortOrder = 50 });
            try
            {
                var fetched = await repository.GetByIdAsync(id);

                Assert.NotNull(fetched);
                Assert.Equal(name, fetched!.Name);
                Assert.Equal(50, fetched.SortOrder);
            }
            finally
            {
                await TestDataHelper.DeleteThaaliSizeAsync(factory, id);
            }
        }

        [Fact]
        public async Task UpdateAsync_ChangesNameAndSortOrder()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliSizeRepository(factory);
            var id = await TestDataHelper.CreateThaaliSizeAsync(factory);
            try
            {
                var newName = "UT_Updated_" + Guid.NewGuid().ToString("N")[..6];

                var updated = await repository.UpdateAsync(id, new UpdateThaaliSizeRequest { Name = newName, SortOrder = 5 });

                Assert.True(updated);
                var fetched = await repository.GetByIdAsync(id);
                Assert.Equal(newName, fetched!.Name);
                Assert.Equal(5, fetched.SortOrder);
            }
            finally
            {
                await TestDataHelper.DeleteThaaliSizeAsync(factory, id);
            }
        }

        [Fact]
        public async Task GetAllAsync_IncludesTheCreatedThaaliSize()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliSizeRepository(factory);
            var id = await TestDataHelper.CreateThaaliSizeAsync(factory);
            try
            {
                var all = await repository.GetAllAsync();

                Assert.Contains(all, s => s.Id == id);
            }
            finally
            {
                await TestDataHelper.DeleteThaaliSizeAsync(factory, id);
            }
        }
        [Fact]
        public async Task DeleteAsync_RemovesAnUnusedThaaliSize()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliSizeRepository(factory);
            var id = await TestDataHelper.CreateThaaliSizeAsync(factory);

            var deleted = await repository.DeleteAsync(id);

            Assert.True(deleted);
            var fetched = await repository.GetByIdAsync(id);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task IsInUseAsync_ReturnsTrueWhenAFamilyReferencesTheSize()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliSizeRepository(factory);
            var sizeId = await TestDataHelper.CreateThaaliSizeAsync(factory);
            var registration = await TestDataHelper.CreateFamilyAsync(factory, sizeId);
            try
            {
                var inUse = await repository.IsInUseAsync(sizeId);

                Assert.True(inUse);
            }
            finally
            {
                await TestDataHelper.DeleteFamilyAsync(factory, registration.FamilyId, registration.UserId);
                await TestDataHelper.DeleteThaaliSizeAsync(factory, sizeId);
            }
        }

        [Fact]
        public async Task IsInUseAsync_ReturnsFalseWhenNoFamilyReferencesTheSize()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new ThaaliSizeRepository(factory);
            var id = await TestDataHelper.CreateThaaliSizeAsync(factory);
            try
            {
                var inUse = await repository.IsInUseAsync(id);

                Assert.False(inUse);
            }
            finally
            {
                await TestDataHelper.DeleteThaaliSizeAsync(factory, id);
            }
        }
    }
}
