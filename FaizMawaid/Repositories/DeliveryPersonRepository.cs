using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class DeliveryPersonRepository : IDeliveryPersonRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DeliveryPersonRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns = @"
            Id, AreaId, FullName, MobileNumber, IsActive, CreatedByUserId, CreatedAt, UpdatedAt";

        public async Task<IEnumerable<DeliveryPerson>> GetAllAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM DeliveryPersons ORDER BY AreaId, FullName;";
            return await connection.QueryAsync<DeliveryPerson>(sql);
        }

        public async Task<DeliveryPerson?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM DeliveryPersons WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<DeliveryPerson>(sql, new { Id = id });
        }

        public async Task<IEnumerable<DeliveryPerson>> GetActiveByAreaIdAsync(byte areaId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM DeliveryPersons WHERE AreaId = @AreaId AND IsActive = 1 ORDER BY FullName;";
            return await connection.QueryAsync<DeliveryPerson>(sql, new { AreaId = areaId });
        }

        public async Task<ulong> CreateAsync(CreateDeliveryPersonRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO DeliveryPersons (AreaId, FullName, MobileNumber, IsActive, CreatedByUserId)
                VALUES (@AreaId, @FullName, @MobileNumber, 1, @CreatedByUserId);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> UpdateAsync(ulong id, UpdateDeliveryPersonRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE DeliveryPersons
                SET AreaId = @AreaId, FullName = @FullName, MobileNumber = @MobileNumber, IsActive = @IsActive, UpdatedAt = UTC_TIMESTAMP()
                WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.AreaId, request.FullName, request.MobileNumber, request.IsActive });
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM DeliveryPersons WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
