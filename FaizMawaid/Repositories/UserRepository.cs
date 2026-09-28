using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns =
            "Id, RoleId, Email, SabilNumber, PasswordHash, MustChangePassword, FullName, Phone, IsActive, CreatedAt, UpdatedAt, LastLoginAt";

        public async Task<User?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Users WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Users WHERE Email = @Email;";
            return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task<User?> GetBySabilNumberAsync(string sabilNumber)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Users WHERE SabilNumber = @SabilNumber;";
            return await connection.QuerySingleOrDefaultAsync<User>(sql, new { SabilNumber = sabilNumber });
        }

        public async Task<IEnumerable<User>> GetAllAsync(byte? roleId = null)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM Users
                WHERE (@RoleId IS NULL OR RoleId = @RoleId)
                ORDER BY FullName;";
            return await connection.QueryAsync<User>(sql, new { RoleId = roleId });
        }

        public async Task<ulong> CreateAsync(CreateUserRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Users (RoleId, Email, SabilNumber, PasswordHash, MustChangePassword, FullName, Phone)
                VALUES (@RoleId, @Email, @SabilNumber, @PasswordHash, @MustChangePassword, @FullName, @Phone);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> UpdateAsync(ulong id, UpdateUserRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET FullName = @FullName, Phone = @Phone, SabilNumber = @SabilNumber WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.FullName, request.Phone, request.SabilNumber });
            return rows > 0;
        }

        public async Task<bool> SetActiveAsync(ulong id, bool isActive)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
            return rows > 0;
        }

        public async Task<bool> SetPasswordAsync(ulong id, string passwordHash, bool mustChangePassword)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Users SET PasswordHash = @PasswordHash, MustChangePassword = @MustChangePassword WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, PasswordHash = passwordHash, MustChangePassword = mustChangePassword });
            return rows > 0;
        }
    }
}
