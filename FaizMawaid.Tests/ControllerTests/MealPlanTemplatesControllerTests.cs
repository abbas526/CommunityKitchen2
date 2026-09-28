using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class MealPlanTemplatesControllerTests
    {
        [Fact]
        public async Task GetAll_ReturnsOkWithTemplates()
        {
            var repo = new Mock<IMealPlanTemplateRepository>();
            var templates = new List<MealPlanTemplate> { new() { Id = 1, Name = "Khichdi & Kadhi", MealDescription = "Khichdi, Kadhi, Papad" } };
            repo.Setup(r => r.GetAllAsync()).ReturnsAsync(templates);
            var controller = new MealPlanTemplatesController(repo.Object);

            var result = await controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(templates, okResult.Value);
        }

        [Fact]
        public async Task Create_ReturnsCreatedAtActionWhenValid()
        {
            var repo = new Mock<IMealPlanTemplateRepository>();
            var request = new CreateMealPlanTemplateRequest { Name = "Biryani & Raita", MealDescription = "Veg Biryani, Raita, Salad", SortOrder = 1, CreatedByUserId = 9 };
            repo.Setup(r => r.CreateAsync(request)).ReturnsAsync((ushort)3);
            var created = new MealPlanTemplate { Id = 3, Name = request.Name, MealDescription = request.MealDescription };
            repo.Setup(r => r.GetByIdAsync((ushort)3)).ReturnsAsync(created);
            var controller = new MealPlanTemplatesController(repo.Object);

            var result = await controller.Create(request);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(created, createdResult.Value);
        }

        [Fact]
        public async Task Create_ReturnsBadRequestWhenNameIsMissing()
        {
            var repo = new Mock<IMealPlanTemplateRepository>();
            var controller = new MealPlanTemplatesController(repo.Object);
            var request = new CreateMealPlanTemplateRequest { Name = " ", MealDescription = "Something", CreatedByUserId = 9 };

            var result = await controller.Create(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_ReturnsNoContentWhenUpdated()
        {
            var repo = new Mock<IMealPlanTemplateRepository>();
            var request = new UpdateMealPlanTemplateRequest { Name = "Updated", MealDescription = "Updated desc", SortOrder = 2 };
            repo.Setup(r => r.UpdateAsync((ushort)5, request)).ReturnsAsync(true);
            var controller = new MealPlanTemplatesController(repo.Object);

            var result = await controller.Update(5, request);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_ReturnsNoContentWhenDeleted()
        {
            var repo = new Mock<IMealPlanTemplateRepository>();
            repo.Setup(r => r.DeleteAsync((ushort)5)).ReturnsAsync(true);
            var controller = new MealPlanTemplatesController(repo.Object);

            var result = await controller.Delete(5);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_ReturnsNotFoundWhenMissing()
        {
            var repo = new Mock<IMealPlanTemplateRepository>();
            repo.Setup(r => r.DeleteAsync((ushort)99)).ReturnsAsync(false);
            var controller = new MealPlanTemplatesController(repo.Object);

            var result = await controller.Delete(99);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
