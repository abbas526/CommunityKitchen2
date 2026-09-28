using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class UserRepositoryTests
    {
        [Fact]
        public async Task CreateAsync_ThenGetByIdAsync_ReturnsTheCreatedUser()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new UserRepository(factory);
            var email = $"ut_{Guid.NewGuid():N}@unittest.local";

            var id = await repository.CreateAsync(new CreateUserRequest
            {
                RoleId = RoleIds.Admin,
                Email = email,
                PasswordHash = "hash",
                FullName = "UT Admin",
                Phone = "1234567890"
            });
            try
            {
                var fetched = await repository.GetByIdAsync(id);

                Assert.NotNull(fetched);
                Assert.Equal(email, fetched!.Email);
                Assert.Equal("UT Admin", fetched.FullName);
                Assert.True(fetched.IsActive);
            }
            finally
            {
                await TestDataHelper.DeleteUserAsync(factory, id);
            }
        }

        [Fact]
        public async Task GetByEmailAsync_ReturnsTheMatchingUser()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new UserRepository(factory);
            var id = await TestDataHelper.CreateUserAsync(factory);
            var created = await repository.GetByIdAsync(id);
            try
            {
                var fetched = await repository.GetByEmailAsync(created!.Email);

                Assert.NotNull(fetched);
                Assert.Equal(id, fetched!.Id);
            }
            finally
            {
                await TestDataHelper.DeleteUserAsync(factory, id);
            }
        }

        [Fact]
        public async Task UpdateAsync_ChangesFullNameAndPhone()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new UserRepository(factory);
            var id = await TestDataHelper.CreateUserAsync(factory);
            try
            {
                var updated = await repository.UpdateAsync(id, new UpdateUserRequest { FullName = "Updated Name", Phone = "9998887777" });

                Assert.True(updated);
                var fetched = await repository.GetByIdAsync(id);
                Assert.Equal("Updated Name", fetched!.FullName);
                Assert.Equal("9998887777", fetched.Phone);
            }
            finally
            {
                await TestDataHelper.DeleteUserAsync(factory, id);
            }
        }

        [Fact]
        public async Task SetActiveAsync_DeactivatesTheUser()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new UserRepository(factory);
            var id = await TestDataHelper.CreateUserAsync(factory);
            try
            {
                var deactivated = await repository.SetActiveAsync(id, false);

                Assert.True(deactivated);
                var fetched = await repository.GetByIdAsync(id);
                Assert.False(fetched!.IsActive);
            }
            finally
            {
                await TestDataHelper.DeleteUserAsync(factory, id);
            }
        }
    }
}
