using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class FamilyRepository : IFamilyRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public FamilyRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns = @"
            Id, FamilyHeadUserId, ParentFamilyId, SubFamilyLabel, Address, AreaId, NumberOfMembers, ThaaliSizeId, RegistrationStatus,
            ApprovedByAdminUserId, ApprovedAt, IsActive, CreatedAt, UpdatedAt";

        public async Task<Family?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Families WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<Family>(sql, new { Id = id });
        }

        public async Task<Family?> GetByFamilyHeadUserIdAsync(ulong userId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Families WHERE FamilyHeadUserId = @UserId;";
            return await connection.QuerySingleOrDefaultAsync<Family>(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<Family>> GetAllAsync(RegistrationStatus? status = null)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM Families
                WHERE ParentFamilyId IS NULL AND (@Status IS NULL OR RegistrationStatus = @Status)
                ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<Family>(sql, new { Status = status?.ToString() });
        }

        public async Task<IEnumerable<Family>> GetActiveApprovedAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM Families
                WHERE RegistrationStatus = 'Approved' AND IsActive = 1;";
            return await connection.QueryAsync<Family>(sql);
        }

        public async Task<IEnumerable<Family>> GetSubFamiliesAsync(ulong parentFamilyId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM Families WHERE ParentFamilyId = @ParentFamilyId ORDER BY CreatedAt;";
            return await connection.QueryAsync<Family>(sql, new { ParentFamilyId = parentFamilyId });
        }

        /// <summary>
        /// Creates the FamilyHead User row, the Family row (Pending), and the opening
        /// FamilySizeHistory row together in one transaction, so history is complete
        /// from day one rather than only starting at the first later change.
        /// </summary>
        public async Task<RegisterFamilyResponse> RegisterAsync(RegisterFamilyRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                const string insertUserSql = @"
                    INSERT INTO Users (RoleId, Email, SabilNumber, PasswordHash, MustChangePassword, FullName, Phone)
                    VALUES (@RoleId, @Email, @SabilNumber, @PasswordHash, 0, @FullName, @Phone);
                    SELECT LAST_INSERT_ID();";
                var userId = await connection.ExecuteScalarAsync<ulong>(insertUserSql, new
                {
                    RoleId = RoleIds.FamilyHead,
                    request.Email,
                    request.SabilNumber,
                    request.PasswordHash,
                    request.FullName,
                    request.Phone
                }, transaction);

                const string insertFamilySql = @"
                    INSERT INTO Families (FamilyHeadUserId, Address, AreaId, NumberOfMembers, ThaaliSizeId, RegistrationStatus)
                    VALUES (@FamilyHeadUserId, @Address, @AreaId, @NumberOfMembers, @ThaaliSizeId, 'Pending');
                    SELECT LAST_INSERT_ID();";
                var familyId = await connection.ExecuteScalarAsync<ulong>(insertFamilySql, new
                {
                    FamilyHeadUserId = userId,
                    request.Address,
                    request.AreaId,
                    request.NumberOfMembers,
                    request.ThaaliSizeId
                }, transaction);

                const string insertHistorySql = @"
                    INSERT INTO FamilySizeHistory (FamilyId, ThaaliSizeId, EffectiveFromDate, ChangedByUserId)
                    VALUES (@FamilyId, @ThaaliSizeId, @EffectiveFromDate, @ChangedByUserId);";
                await connection.ExecuteAsync(insertHistorySql, new
                {
                    FamilyId = familyId,
                    request.ThaaliSizeId,
                    EffectiveFromDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    ChangedByUserId = userId
                }, transaction);

                transaction.Commit();
                return new RegisterFamilyResponse { UserId = userId, FamilyId = familyId };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Admin-only: links a new sub-family (its own Family row, no User of its own) to an
        /// existing primary family, together with its opening FamilySizeHistory row, in one
        /// transaction. Created already Approved/active -- an Admin is vouching for it directly.
        /// </summary>
        public async Task<ulong> CreateSubFamilyAsync(ulong parentFamilyId, CreateSubFamilyRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                const string insertFamilySql = @"
                    INSERT INTO Families (ParentFamilyId, SubFamilyLabel, Address, NumberOfMembers, ThaaliSizeId, RegistrationStatus, ApprovedByAdminUserId, ApprovedAt, IsActive)
                    VALUES (@ParentFamilyId, @SubFamilyLabel, @Address, @NumberOfMembers, @ThaaliSizeId, 'Approved', @CreatedByAdminUserId, UTC_TIMESTAMP(), 1);
                    SELECT LAST_INSERT_ID();";
                var subFamilyId = await connection.ExecuteScalarAsync<ulong>(insertFamilySql, new
                {
                    ParentFamilyId = parentFamilyId,
                    request.SubFamilyLabel,
                    request.Address,
                    request.NumberOfMembers,
                    request.ThaaliSizeId,
                    request.CreatedByAdminUserId
                }, transaction);

                const string insertHistorySql = @"
                    INSERT INTO FamilySizeHistory (FamilyId, ThaaliSizeId, EffectiveFromDate, ChangedByUserId)
                    VALUES (@FamilyId, @ThaaliSizeId, @EffectiveFromDate, @ChangedByUserId);";
                await connection.ExecuteAsync(insertHistorySql, new
                {
                    FamilyId = subFamilyId,
                    request.ThaaliSizeId,
                    EffectiveFromDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    ChangedByUserId = request.CreatedByAdminUserId
                }, transaction);

                transaction.Commit();
                return subFamilyId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> ApproveAsync(ulong familyId, ulong approvedByAdminUserId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Families
                SET RegistrationStatus = 'Approved',
                    ApprovedByAdminUserId = @ApprovedByAdminUserId,
                    ApprovedAt = UTC_TIMESTAMP(),
                    IsActive = 1
                WHERE Id = @FamilyId AND RegistrationStatus = 'Pending';";
            var rows = await connection.ExecuteAsync(sql, new { FamilyId = familyId, ApprovedByAdminUserId = approvedByAdminUserId });
            return rows > 0;
        }

        /// <summary>Reuses ApprovedByAdminUserId/ApprovedAt to record who processed the rejection and when -- there's no separate Rejected-by column in the schema.</summary>
        public async Task<bool> RejectAsync(ulong familyId, ulong rejectedByAdminUserId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Families
                SET RegistrationStatus = 'Rejected',
                    ApprovedByAdminUserId = @RejectedByAdminUserId,
                    ApprovedAt = UTC_TIMESTAMP(),
                    IsActive = 0
                WHERE Id = @FamilyId AND RegistrationStatus = 'Pending';";
            var rows = await connection.ExecuteAsync(sql, new { FamilyId = familyId, RejectedByAdminUserId = rejectedByAdminUserId });
            return rows > 0;
        }

        public async Task<bool> UpdateAsync(ulong familyId, UpdateFamilyRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Families SET Address = @Address, AreaId = @AreaId, NumberOfMembers = @NumberOfMembers WHERE Id = @FamilyId;";
            var rows = await connection.ExecuteAsync(sql, new { FamilyId = familyId, request.Address, request.AreaId, request.NumberOfMembers });
            return rows > 0;
        }

        /// <summary>Closes the currently-open FamilySizeHistory row, inserts the new one, and updates Families.ThaaliSizeId -- all in one transaction.</summary>
        public async Task<bool> ChangeThaaliSizeAsync(ulong familyId, ChangeThaaliSizeRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                const string closeHistorySql = @"
                    UPDATE FamilySizeHistory
                    SET EffectiveToDate = @EffectiveToDate
                    WHERE FamilyId = @FamilyId AND EffectiveToDate IS NULL;";
                await connection.ExecuteAsync(closeHistorySql, new
                {
                    FamilyId = familyId,
                    EffectiveToDate = request.EffectiveFromDate.AddDays(-1)
                }, transaction);

                const string insertHistorySql = @"
                    INSERT INTO FamilySizeHistory (FamilyId, ThaaliSizeId, EffectiveFromDate, ChangedByUserId)
                    VALUES (@FamilyId, @ThaaliSizeId, @EffectiveFromDate, @ChangedByUserId);";
                await connection.ExecuteAsync(insertHistorySql, new
                {
                    FamilyId = familyId,
                    ThaaliSizeId = request.NewThaaliSizeId,
                    request.EffectiveFromDate,
                    ChangedByUserId = request.ChangedByUserId
                }, transaction);

                const string updateFamilySql = "UPDATE Families SET ThaaliSizeId = @ThaaliSizeId WHERE Id = @FamilyId;";
                var rows = await connection.ExecuteAsync(updateFamilySql, new
                {
                    FamilyId = familyId,
                    ThaaliSizeId = request.NewThaaliSizeId
                }, transaction);

                transaction.Commit();
                return rows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> SetActiveAsync(ulong familyId, bool isActive)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE Families SET IsActive = @IsActive WHERE Id = @Id;";
            var rows = await connection.ExecuteAsync(sql, new { Id = familyId, IsActive = isActive });
            return rows > 0;
        }

        /// <summary>
        /// Bulk-import path: creates the FamilyHead User row, the Family row (already Approved
        /// and active), and the opening FamilySizeHistory row together in one transaction --
        /// the same shape as RegisterAsync, but skipping the Pending review queue since the
        /// Admin is vouching for every row in the sheet directly.
        /// </summary>
        public async Task<RegisterFamilyResponse> ImportApprovedFamilyAsync(RegisterFamilyRequest request, ulong approvedByAdminUserId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                const string insertUserSql = @"
                    INSERT INTO Users (RoleId, Email, SabilNumber, PasswordHash, MustChangePassword, FullName, Phone)
                    VALUES (@RoleId, @Email, @SabilNumber, @PasswordHash, 1, @FullName, @Phone);
                    SELECT LAST_INSERT_ID();";
                var userId = await connection.ExecuteScalarAsync<ulong>(insertUserSql, new
                {
                    RoleId = RoleIds.FamilyHead,
                    request.Email,
                    request.SabilNumber,
                    request.PasswordHash,
                    request.FullName,
                    request.Phone
                }, transaction);

                const string insertFamilySql = @"
                    INSERT INTO Families (FamilyHeadUserId, Address, AreaId, NumberOfMembers, ThaaliSizeId, RegistrationStatus, ApprovedByAdminUserId, ApprovedAt, IsActive)
                    VALUES (@FamilyHeadUserId, @Address, @AreaId, @NumberOfMembers, @ThaaliSizeId, 'Approved', @ApprovedByAdminUserId, UTC_TIMESTAMP(), 1);
                    SELECT LAST_INSERT_ID();";
                var familyId = await connection.ExecuteScalarAsync<ulong>(insertFamilySql, new
                {
                    FamilyHeadUserId = userId,
                    request.Address,
                    request.AreaId,
                    request.NumberOfMembers,
                    request.ThaaliSizeId,
                    ApprovedByAdminUserId = approvedByAdminUserId
                }, transaction);

                const string insertHistorySql = @"
                    INSERT INTO FamilySizeHistory (FamilyId, ThaaliSizeId, EffectiveFromDate, ChangedByUserId)
                    VALUES (@FamilyId, @ThaaliSizeId, @EffectiveFromDate, @ChangedByUserId);";
                await connection.ExecuteAsync(insertHistorySql, new
                {
                    FamilyId = familyId,
                    request.ThaaliSizeId,
                    EffectiveFromDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    ChangedByUserId = approvedByAdminUserId
                }, transaction);

                transaction.Commit();
                return new RegisterFamilyResponse { UserId = userId, FamilyId = familyId };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
