using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class MealPlanTemplateRepository : IMealPlanTemplateRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MealPlanTemplateRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns =
            "Id, Name, MealDescription, SortOrder, CreatedByUserId, CreatedAt, UpdatedAt";

        public async Task<IEnumerable<MealPlanTemplate>> GetAllAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM MealPlanTemplates ORDER BY SortOrder, Name;";
            return await connection.QueryAsync<MealPlanTemplate>(sql);
        }

        public async Task<MealPlanTemplate?> GetByIdAsync(ushort id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM MealPlanTemplates WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<MealPlanTemplate>(sql, new { Id = id });
        }

        public async Task<ushort> CreateAsync(CreateMealPlanTemplateRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO MealPlanTemplates (Name, MealDescription, SortOrder, CreatedByUserId)
                VALUES (@Name, @MealDescription, @SortOrder, @CreatedByUserId);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ushort>(sql, request);
        }

        public async Task<bool> UpdateAsync(ushort id, UpdateMealPlanTemplateRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE MealPlanTemplates SET Name = @Name, MealDescription = @MealDescription, SortOrder = @SortOrder WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.Name, request.MealDescription, request.SortOrder });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(ushort id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM MealPlanTemplates WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
