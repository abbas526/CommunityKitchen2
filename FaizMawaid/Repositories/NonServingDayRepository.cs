using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class NonServingDayRepository : INonServingDayRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public NonServingDayRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<NonServingDay>> GetAllAsync(int? year = null)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT Id, TheDate, Reason, CreatedByUserId, CreatedAt
                FROM NonServingDays
                WHERE (@Year IS NULL OR YEAR(TheDate) = @Year)
                ORDER BY TheDate;";
            return await connection.QueryAsync<NonServingDay>(sql, new { Year = year });
        }

        public async Task<bool> IsNonServingDayAsync(DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM NonServingDays WHERE TheDate = @Date;";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { Date = date });
            return count > 0;
        }

        public async Task<uint> CreateAsync(CreateNonServingDayRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO NonServingDays (TheDate, Reason, CreatedByUserId)
                VALUES (@TheDate, @Reason, @CreatedByUserId);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<uint>(sql, request);
        }

        public async Task<bool> DeleteAsync(uint id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM NonServingDays WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
