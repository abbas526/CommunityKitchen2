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

        public async Task<int> CountActiveByRoleAsync(byte roleId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(*) FROM Users WHERE RoleId = @RoleId AND IsActive = 1;";
            return await connection.ExecuteScalarAsync<int>(sql, new { RoleId = roleId });
        }

        // ---- SuperAdmin seat rules (max RoleIds.MaxActiveSuperAdmins ACTIVE SuperAdmins; never zero) ----
        // Every method below takes the same lock first -- the SuperAdmin row of Roles -- so any two
        // requests that could change the number of active SuperAdmins run one after the other, and the
        // COUNT they read is still true when they write. The lock is always taken BEFORE any Users row
        // is locked, in every method, so they can't deadlock each other.

        private static async Task LockSuperAdminSeatsAsync(IDbConnection connection, IDbTransaction transaction)
        {
            const string sql = "SELECT Id FROM Roles WHERE Id = @RoleId FOR UPDATE;";
            await connection.ExecuteScalarAsync<byte?>(sql, new { RoleId = RoleIds.SuperAdmin }, transaction);
        }

        private static async Task<int> CountActiveSuperAdminsAsync(IDbConnection connection, IDbTransaction transaction)
        {
            const string sql = "SELECT COUNT(*) FROM Users WHERE RoleId = @RoleId AND IsActive = 1;";
            return await connection.ExecuteScalarAsync<int>(sql, new { RoleId = RoleIds.SuperAdmin }, transaction);
        }

        private sealed class RoleAndActiveRow
        {
            public byte RoleId { get; set; }
            public bool IsActive { get; set; }
        }

        private static async Task<RoleAndActiveRow?> LoadRoleAndActiveForUpdateAsync(IDbConnection connection, IDbTransaction transaction, ulong id)
        {
            const string sql = "SELECT RoleId, IsActive FROM Users WHERE Id = @Id FOR UPDATE;";
            return await connection.QuerySingleOrDefaultAsync<RoleAndActiveRow>(sql, new { Id = id }, transaction);
        }

        public async Task<UserCreateResult> CreateSuperAdminAsync(CreateUserRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                await LockSuperAdminSeatsAsync(connection, transaction);
                var active = await CountActiveSuperAdminsAsync(connection, transaction);
                if (active >= RoleIds.MaxActiveSuperAdmins)
                {
                    transaction.Rollback();
                    return new UserCreateResult { Result = UserChangeResult.SeatsFull };
                }

                request.RoleId = RoleIds.SuperAdmin;
                const string insertSql = @"
                    INSERT INTO Users (RoleId, Email, SabilNumber, PasswordHash, MustChangePassword, FullName, Phone)
                    VALUES (@RoleId, @Email, @SabilNumber, @PasswordHash, @MustChangePassword, @FullName, @Phone);
                    SELECT LAST_INSERT_ID();";
                var id = await connection.ExecuteScalarAsync<ulong>(insertSql, request, transaction);

                transaction.Commit();
                return new UserCreateResult { Result = UserChangeResult.Ok, UserId = id };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<UserChangeResult> SetRoleAsync(ulong id, byte newRoleId)
        {
            if (newRoleId != RoleIds.Admin && newRoleId != RoleIds.SuperAdmin)
            {
                throw new ArgumentOutOfRangeException(nameof(newRoleId), "Only Admin and SuperAdmin are valid targets for SetRoleAsync.");
            }

            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                await LockSuperAdminSeatsAsync(connection, transaction);
                var target = await LoadRoleAndActiveForUpdateAsync(connection, transaction, id);
                if (target is null)
                {
                    transaction.Rollback();
                    return UserChangeResult.NotFound;
                }
                if (target.RoleId == newRoleId)
                {
                    transaction.Rollback();
                    return UserChangeResult.Ok;
                }

                var active = await CountActiveSuperAdminsAsync(connection, transaction);
                if (newRoleId == RoleIds.SuperAdmin && target.IsActive && active >= RoleIds.MaxActiveSuperAdmins)
                {
                    transaction.Rollback();
                    return UserChangeResult.SeatsFull;
                }
                if (target.RoleId == RoleIds.SuperAdmin && target.IsActive && active <= 1)
                {
                    transaction.Rollback();
                    return UserChangeResult.WouldLeaveNoSuperAdmin;
                }

                const string updateSql = "UPDATE Users SET RoleId = @RoleId WHERE Id = @Id;";
                await connection.ExecuteAsync(updateSql, new { Id = id, RoleId = newRoleId }, transaction);

                transaction.Commit();
                return UserChangeResult.Ok;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<UserChangeResult> SetActiveGuardedAsync(ulong id, bool isActive)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                await LockSuperAdminSeatsAsync(connection, transaction);
                var target = await LoadRoleAndActiveForUpdateAsync(connection, transaction, id);
                if (target is null)
                {
                    transaction.Rollback();
                    return UserChangeResult.NotFound;
                }
                if (target.IsActive == isActive)
                {
                    transaction.Rollback();
                    return UserChangeResult.Ok;
                }

                if (target.RoleId == RoleIds.SuperAdmin)
                {
                    var active = await CountActiveSuperAdminsAsync(connection, transaction);
                    if (isActive && active >= RoleIds.MaxActiveSuperAdmins)
                    {
                        transaction.Rollback();
                        return UserChangeResult.SeatsFull;
                    }
                    if (!isActive && active <= 1)
                    {
                        transaction.Rollback();
                        return UserChangeResult.WouldLeaveNoSuperAdmin;
                    }
                }

                const string updateSql = "UPDATE Users SET IsActive = @IsActive WHERE Id = @Id;";
                await connection.ExecuteAsync(updateSql, new { Id = id, IsActive = isActive }, transaction);

                transaction.Commit();
                return UserChangeResult.Ok;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
