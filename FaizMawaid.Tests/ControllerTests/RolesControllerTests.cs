using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class RolesControllerTests
    {
        [Fact]
        public async Task GetAll_ReturnsOkWithRolesFromRepository()
        {
            var mockRepo = new Mock<IRoleRepository>();
            var roles = new List<Role>
            {
                new() { Id = RoleIds.Admin, Name = "Admin" },
                new() { Id = RoleIds.FamilyHead, Name = "FamilyHead" }
            };
            mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(roles);
            var controller = new RolesController(mockRepo.Object);

            var result = await controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(roles, okResult.Value);
        }
    }
}
