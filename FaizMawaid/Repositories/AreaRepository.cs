using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class AreaRepository : IAreaRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AreaRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<Area>> GetAllAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT Id, Name, SortOrder FROM Areas ORDER BY SortOrder;";
            return await connection.QueryAsync<Area>(sql);
        }

        public async Task<Area?> GetByIdAsync(byte id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT Id, Name, SortOrder FROM Areas WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<Area>(sql, new { Id = id });
        }

        public async Task<byte> CreateAsync(CreateAreaRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Areas (Name, SortOrder) VALUES (@Name, @SortOrder);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<byte>(sql, request);
        }

        public async Task<bool> UpdateAsync(byte id, UpdateAreaRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Areas SET Name = @Name, SortOrder = @SortOrder WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.Name, request.SortOrder });
            return rows > 0;
        }

        public async Task<bool> IsInUseAsync(byte id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    (SELECT COUNT(1) FROM Families WHERE AreaId = @Id) +
                    (SELECT COUNT(1) FROM DeliveryPersonAreas WHERE AreaId = @Id);";
            var count = await connection.ExecuteScalarAsync<long>(sql, new { Id = id });
            return count > 0;
        }

        public async Task<bool> DeleteAsync(byte id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM Areas WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
