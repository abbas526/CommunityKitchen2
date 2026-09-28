using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class ThaaliSizeRepository : IThaaliSizeRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ThaaliSizeRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<ThaaliSize>> GetAllAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT Id, Name, SortOrder FROM ThaaliSizes ORDER BY SortOrder;";
            return await connection.QueryAsync<ThaaliSize>(sql);
        }

        public async Task<ThaaliSize?> GetByIdAsync(byte id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT Id, Name, SortOrder FROM ThaaliSizes WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<ThaaliSize>(sql, new { Id = id });
        }

        public async Task<byte> CreateAsync(CreateThaaliSizeRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO ThaaliSizes (Name, SortOrder) VALUES (@Name, @SortOrder);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<byte>(sql, request);
        }

        public async Task<bool> UpdateAsync(byte id, UpdateThaaliSizeRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE ThaaliSizes SET Name = @Name, SortOrder = @SortOrder WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.Name, request.SortOrder });
            return rows > 0;
        }

        public async Task<bool> IsInUseAsync(byte id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT
                    (SELECT COUNT(1) FROM Families WHERE ThaaliSizeId = @Id) +
                    (SELECT COUNT(1) FROM FamilySizeHistory WHERE ThaaliSizeId = @Id);";
            var count = await connection.ExecuteScalarAsync<long>(sql, new { Id = id });
            return count > 0;
        }

        public async Task<bool> DeleteAsync(byte id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM ThaaliSizes WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
