using FaizMawaid.Models;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class RoleRepositoryTests
    {
        [Fact]
        public async Task GetAllAsync_ReturnsSeededAdminAndFamilyHeadRoles()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new RoleRepository(factory);

            var roles = await repository.GetAllAsync();

            Assert.Contains(roles, r => r.Id == RoleIds.Admin && r.Name == "Admin");
            Assert.Contains(roles, r => r.Id == RoleIds.FamilyHead && r.Name == "FamilyHead");
        }
    }
}
