using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public FeedbackRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns =
            "Id, FamilyId, CreatedByUserId, Message, Status, ResponseText, RespondedByAdminUserId, RespondedAt, CreatedAt";

        public async Task<Feedback?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Feedback WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<Feedback>(sql, new { Id = id });
        }

        public async Task<IEnumerable<Feedback>> GetByFamilyIdAsync(ulong familyId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Feedback WHERE FamilyId = @FamilyId ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<Feedback>(sql, new { FamilyId = familyId });
        }

        public async Task<IEnumerable<Feedback>> GetAllAsync(FeedbackStatus? status)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM Feedback
                WHERE (@Status IS NULL OR Status = @Status)
                ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<Feedback>(sql, new { Status = status?.ToString() });
        }

        public async Task<int> CountOpenAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM Feedback WHERE Status = 'Open';";
            return await connection.ExecuteScalarAsync<int>(sql);
        }

        public async Task<ulong> CreateAsync(CreateFeedbackRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Feedback (FamilyId, CreatedByUserId, Message, Status)
                VALUES (@FamilyId, @CreatedByUserId, @Message, 'Open');
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> RespondAsync(ulong id, RespondFeedbackRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Feedback
                SET ResponseText = @ResponseText,
                    RespondedByAdminUserId = @RespondedByAdminUserId,
                    RespondedAt = UTC_TIMESTAMP(),
                    Status = 'Responded'
                WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.ResponseText, request.RespondedByAdminUserId });
            return rows > 0;
        }
    }
}
