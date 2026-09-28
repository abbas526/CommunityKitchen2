using FaizMawaid.Controllers;
using FaizMawaid.Models;
using FaizMawaid.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FaizMawaid.Tests.ControllerTests
{
    public class AuditLogsControllerTests
    {
        [Fact]
        public async Task Get_ReturnsOkWithMatchingLogs()
        {
            var auditRepo = new Mock<IAuditLogRepository>();
            var logs = new List<AuditLog> { new() { Id = 1, Action = "Test", EntityType = "Family" } };
            auditRepo.Setup(r => r.GetAsync(null, null, null, null)).ReturnsAsync(logs);
            var controller = new AuditLogsController(auditRepo.Object);

            var result = await controller.Get(null, null, null, null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(logs, okResult.Value);
        }
    }
}
