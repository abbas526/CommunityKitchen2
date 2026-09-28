using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class AppSettingsRepository : IAppSettingsRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AppSettingsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<AppSetting> GetAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT Id, MealVisibilityDays, UpdatedByUserId, UpdatedAt FROM AppSettings WHERE Id = 1;";
            var settings = await connection.QuerySingleOrDefaultAsync<AppSetting>(sql);
            return settings ?? new AppSetting { Id = 1, MealVisibilityDays = 7 };
        }

        public async Task<bool> UpdateAsync(UpdateAppSettingsRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE AppSettings
                SET MealVisibilityDays = @MealVisibilityDays, UpdatedByUserId = @UpdatedByUserId
                WHERE Id = 1;";
            var rows = await connection.ExecuteAsync(sql, request);
            return rows > 0;
        }
    }
}
