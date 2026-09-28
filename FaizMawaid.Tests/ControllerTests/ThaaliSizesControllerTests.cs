using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class ThaaliSizesControllerTests
    {
        [Fact]
        public async Task GetAll_ReturnsOkWithSizes()
        {
            var repo = new Mock<IThaaliSizeRepository>();
            var sizes = new List<ThaaliSize> { new() { Id = 1, Name = "Small", SortOrder = 1 } };
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(sizes);
            var controller = new ThaaliSizesController(repo.Object);

            var result = await controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(sizes, okResult.Value);
        }

        [Fact]
        public async Task Create_ReturnsCreatedAtAction()
        {
            var repo = new Mock<IThaaliSizeRepository>();
            var request = new CreateThaaliSizeRequest { Name = "Large", SortOrder = 3 };
            repo.Setup(r => r.CreateAsync(request)).ReturnsAsync((byte)3);
            var created = new ThaaliSize { Id = 3, Name = "Large", SortOrder = 3 };
            repo.Setup(r => r.GetByIdAsync((byte)3)).ReturnsAsync(created);
            var controller = new ThaaliSizesController(repo.Object);

            var result = await controller.Create(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
        }

        [Fact]
        public async Task Update_ReturnsNoContentWhenUpdated()
        {
            var repo = new Mock<IThaaliSizeRepository>();
            var request = new UpdateThaaliSizeRequest { Name = "Medium+", SortOrder = 2 };
            repo.Setup(r => r.UpdateAsync((byte)2, request)).ReturnsAsync(true);
            var controller = new ThaaliSizesController(repo.Object);

            var result = await controller.Update(2, request);

            Assert.IsType<NoContentResult>(result);
        }
        [Fact]
        public async Task Delete_ReturnsNoContentWhenUnused()
        {
            var repo = new Mock<IThaaliSizeRepository>();
            var size = new ThaaliSize { Id = 4, Name = "Small", SortOrder = 1 };
            repo.Setup(r => r.GetByIdAsync((byte)4)).ReturnsAsync(size);
            repo.Setup(r => r.IsInUseAsync((byte)4)).ReturnsAsync(false);
            repo.Setup(r => r.DeleteAsync((byte)4)).ReturnsAsync(true);
            var controller = new ThaaliSizesController(repo.Object);

            var result = await controller.Delete(4);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_ReturnsConflictWhenInUse()
        {
            var repo = new Mock<IThaaliSizeRepository>();
            var size = new ThaaliSize { Id = 4, Name = "Small", SortOrder = 1 };
            repo.Setup(r => r.GetByIdAsync((byte)4)).ReturnsAsync(size);
            repo.Setup(r => r.IsInUseAsync((byte)4)).ReturnsAsync(true);
            var controller = new ThaaliSizesController(repo.Object);

            var result = await controller.Delete(4);

            Assert.IsType<ConflictObjectResult>(result);
            repo.Verify(r => r.DeleteAsync(It.IsAny<byte>()), Times.Never);
        }

        [Fact]
        public async Task Delete_ReturnsNotFoundWhenSizeDoesNotExist()
        {
            var repo = new Mock<IThaaliSizeRepository>();
            repo.Setup(r => r.GetByIdAsync((byte)99)).ReturnsAsync((ThaaliSize?)null);
            var controller = new ThaaliSizesController(repo.Object);

            var result = await controller.Delete(99);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
