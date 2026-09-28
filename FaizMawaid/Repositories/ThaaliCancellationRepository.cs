using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class ThaaliCancellationRepository : IThaaliCancellationRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ThaaliCancellationRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns =
            "Id, FamilyId, StartDate, EndDate, Reason, Status, CreatedByUserId, CreatedAt, ReinstatedAt, ReinstatedByUserId";

        public async Task<ThaaliCancellation?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM ThaaliCancellations WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<ThaaliCancellation>(sql, new { Id = id });
        }

        public async Task<IEnumerable<ThaaliCancellation>> GetByFamilyIdAsync(ulong familyId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM ThaaliCancellations WHERE FamilyId = @FamilyId ORDER BY StartDate DESC;";
            return await connection.QueryAsync<ThaaliCancellation>(sql, new { FamilyId = familyId });
        }

        public async Task<IEnumerable<ThaaliCancellation>> GetByDateRangeAsync(DateOnly from, DateOnly to)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM ThaaliCancellations
                WHERE StartDate <= @To AND EndDate >= @From
                ORDER BY StartDate;";
            return await connection.QueryAsync<ThaaliCancellation>(sql, new { From = from, To = to });
        }

        public async Task<bool> HasActiveCancellationOnDateAsync(ulong familyId, DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT COUNT(1) FROM ThaaliCancellations
                WHERE FamilyId = @FamilyId AND Status = 'Active'
                  AND StartDate <= @Date AND EndDate >= @Date;";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { FamilyId = familyId, Date = date });
            return count > 0;
        }

        public async Task<ulong> CreateAsync(CreateThaaliCancellationRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO ThaaliCancellations (FamilyId, StartDate, EndDate, Reason, Status, CreatedByUserId)
                VALUES (@FamilyId, @StartDate, @EndDate, @Reason, 'Active', @CreatedByUserId);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> ReinstateAsync(ulong id, ulong reinstatedByUserId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE ThaaliCancellations
                SET Status = 'Reinstated', ReinstatedAt = UTC_TIMESTAMP(), ReinstatedByUserId = @ReinstatedByUserId
                WHERE Id = @Id AND Status = 'Active';";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, ReinstatedByUserId = reinstatedByUserId });
            return rows > 0;
        }

        public async Task<IEnumerable<ulong>> GetCancelledFamilyIdsOnDateAsync(DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT DISTINCT FamilyId FROM ThaaliCancellations
                WHERE Status = 'Active' AND StartDate <= @Date AND EndDate >= @Date;";
            return await connection.QueryAsync<ulong>(sql, new { Date = date });
        }
    }
}
