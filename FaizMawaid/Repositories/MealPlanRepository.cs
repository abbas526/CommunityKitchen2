using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class MealPlanRepository : IMealPlanRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MealPlanRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns =
            "Id, MealDate, MealDescription, CreatedByUserId, CreatedAt, UpdatedByUserId, UpdatedAt";

        public async Task<MealPlan?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM MealPlans WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<MealPlan>(sql, new { Id = id });
        }

        public async Task<MealPlan?> GetByDateAsync(DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM MealPlans WHERE MealDate = @Date;";
            return await connection.QuerySingleOrDefaultAsync<MealPlan>(sql, new { Date = date });
        }

        public async Task<IEnumerable<MealPlan>> GetRangeAsync(DateOnly from, DateOnly to)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM MealPlans WHERE MealDate BETWEEN @From AND @To ORDER BY MealDate;";
            return await connection.QueryAsync<MealPlan>(sql, new { From = from, To = to });
        }

        public async Task<ulong> CreateAsync(CreateMealPlanRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO MealPlans (MealDate, MealDescription, CreatedByUserId)
                VALUES (@MealDate, @MealDescription, @CreatedByUserId);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> UpdateAsync(ulong id, UpdateMealPlanRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE MealPlans
                SET MealDescription = @MealDescription, UpdatedByUserId = @UpdatedByUserId
                WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.MealDescription, request.UpdatedByUserId });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM MealPlans WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
