using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class UsersControllerTests
    {
        private static UsersController CreateController(Mock<IUserRepository> userRepo, Mock<IPasswordHasherService>? passwordHasher = null)
        {
            passwordHasher ??= new Mock<IPasswordHasherService>();
            passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
            return new UsersController(userRepo.Object, passwordHasher.Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithUsers()
        {
            var mockRepo = new Mock<IUserRepository>();
            var users = new List<User> { new() { Id = 1, RoleId = RoleIds.Admin, Email = "a@b.com", FullName = "A B" } };
            mockRepo.Setup(r => r.GetAllAsync(null)).ReturnsAsync(users);
            var controller = CreateController(mockRepo);

            var result = await controller.GetAll(null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(users, okResult.Value);
        }

        [Fact]
        public async Task GetById_ReturnsOkWhenUserExists()
        {
            var mockRepo = new Mock<IUserRepository>();
            var user = new User { Id = 5, RoleId = RoleIds.Admin, Email = "a@b.com", FullName = "A B" };
            mockRepo.Setup(r => r.GetByIdAsync(5UL)).ReturnsAsync(user);
            var controller = CreateController(mockRepo);

            var result = await controller.GetById(5);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(user, okResult.Value);
        }

        [Fact]
        public async Task Create_ReturnsCreatedAtActionWhenEmailIsNew()
        {
            var mockRepo = new Mock<IUserRepository>();
            var request = new CreateUserRequest { RoleId = RoleIds.Admin, Email = "new@b.com", Password = "TestPass123", FullName = "New Admin" };
            mockRepo.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);
            mockRepo.Setup(r => r.CreateAsync(request)).ReturnsAsync(42UL);
            var created = new User { Id = 42, RoleId = RoleIds.Admin, Email = request.Email, FullName = request.FullName };
            mockRepo.Setup(r => r.GetByIdAsync(42UL)).ReturnsAsync(created);
            var controller = CreateController(mockRepo);

            var result = await controller.Create(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
            Assert.True(request.MustChangePassword);
        }

        [Fact]
        public async Task Create_ReturnsBadRequestWhenPasswordIsTooShort()
        {
            var mockRepo = new Mock<IUserRepository>();
            var controller = CreateController(mockRepo);
            var request = new CreateUserRequest { RoleId = RoleIds.Admin, Email = "new@b.com", Password = "short", FullName = "New Admin" };

            var result = await controller.Create(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_ReturnsNoContentWhenUpdated()
        {
            var mockRepo = new Mock<IUserRepository>();
            var request = new UpdateUserRequest { FullName = "Updated Name" };
            mockRepo.Setup(r => r.UpdateAsync(7UL, request)).ReturnsAsync(true);
            var controller = CreateController(mockRepo);

            var result = await controller.Update(7, request);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Deactivate_ReturnsNoContentWhenUpdated()
        {
            var mockRepo = new Mock<IUserRepository>();
            mockRepo.Setup(r => r.SetActiveAsync(3UL, false)).ReturnsAsync(true);
            var controller = CreateController(mockRepo);

            var result = await controller.Deactivate(3);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Activate_ReturnsNoContentWhenUpdated()
        {
            var mockRepo = new Mock<IUserRepository>();
            mockRepo.Setup(r => r.SetActiveAsync(3UL, true)).ReturnsAsync(true);
            var controller = CreateController(mockRepo);

            var result = await controller.Activate(3);

            Assert.IsType<NoContentResult>(result);
        }
    }
}
