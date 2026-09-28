using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns =
            "Id, UserId, TokenHash, ExpiresAt, CreatedAt, CreatedByIp, RevokedAt, ReplacedByTokenHash";

        public async Task<ulong> CreateAsync(ulong userId, string tokenHash, DateTime expiresAt, string? createdByIp)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO RefreshTokens (UserId, TokenHash, ExpiresAt, CreatedByIp)
                VALUES (@UserId, @TokenHash, @ExpiresAt, @CreatedByIp);
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, new { UserId = userId, TokenHash = tokenHash, ExpiresAt = expiresAt, CreatedByIp = createdByIp });
        }

        public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM RefreshTokens WHERE TokenHash = @TokenHash;";
            return await connection.QuerySingleOrDefaultAsync<RefreshToken>(sql, new { TokenHash = tokenHash });
        }

        public async Task<bool> RevokeAsync(string tokenHash, string? replacedByTokenHash = null)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE RefreshTokens
                SET RevokedAt = UTC_TIMESTAMP(), ReplacedByTokenHash = @ReplacedByTokenHash
                WHERE TokenHash = @TokenHash AND RevokedAt IS NULL;";
            var rows = await connection.ExecuteAsync(sql, new { TokenHash = tokenHash, ReplacedByTokenHash = replacedByTokenHash });
            return rows > 0;
        }
    }
}
