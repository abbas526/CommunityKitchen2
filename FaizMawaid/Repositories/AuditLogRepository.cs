using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AuditLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<ulong> AddAsync(AuditLog log)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO AuditLogs (UserId, Action, EntityType, EntityId, Metadata)
                VALUES (@UserId, @Action, @EntityType, @EntityId, @MetadataJson);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, log);
        }

        public async Task<IEnumerable<AuditLog>> GetAsync(string? entityType = null, ulong? entityId = null, DateOnly? from = null, DateOnly? to = null)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT Id, UserId, Action, EntityType, EntityId, Metadata AS MetadataJson, CreatedAt
                FROM AuditLogs
                WHERE (@EntityType IS NULL OR EntityType = @EntityType)
                  AND (@EntityId IS NULL OR EntityId = @EntityId)
                  AND (@From IS NULL OR CreatedAt >= @From)
                  AND (@ToExclusive IS NULL OR CreatedAt < @ToExclusive)
                ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<AuditLog>(sql, new
            {
                EntityType = entityType,
                EntityId = entityId,
                From = from,
                ToExclusive = to?.AddDays(1)
            });
        }
    }
}
