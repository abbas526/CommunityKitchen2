using Dapper;
using FaizMawaid.Models;
using FaizMawaid.Repositories;
using Xunit;

namespace FaizMawaid.Tests.RepositoryTests
{
    public class AuditLogRepositoryTests
    {
        [Fact]
        public async Task AddAsync_ThenGetAsync_ReturnsTheLoggedEntry()
        {
            var factory = TestConnectionFactory.Create();
            var repository = new AuditLogRepository(factory);
            var entityId = (ulong)Random.Shared.Next(1_000_000, 9_999_999);
            var entityType = "UnitTestEntity_" + Guid.NewGuid().ToString("N")[..8];
            ulong id = 0;
            try
            {
                id = await repository.AddAsync(new AuditLog
                {
                    UserId = null,
                    Action = "UnitTestAction",
                    EntityType = entityType,
                    EntityId = entityId,
                    CreatedAt = DateTime.UtcNow
                });

                var results = await repository.GetAsync(entityType: entityType, entityId: entityId);

                Assert.Single(results);
                Assert.Equal("UnitTestAction", results.First().Action);
            }
            finally
            {
                if (id != 0)
                {
                    using var connection = factory.CreateConnection();
                    await connection.ExecuteAsync("DELETE FROM AuditLogs WHERE Id = @Id;", new { Id = id });
                }
            }
        }
    }
}
