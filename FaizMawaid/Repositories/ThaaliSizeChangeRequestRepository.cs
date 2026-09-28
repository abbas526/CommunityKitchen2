using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class ThaaliSizeChangeRequestRepository : IThaaliSizeChangeRequestRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ThaaliSizeChangeRequestRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns = @"
            Id, FamilyId, NewThaaliSizeId, EffectiveFromDate, Reason, Status, CreatedByUserId, CreatedAt,
            ReviewedByAdminUserId, ReviewedAt, AdminNote, FamilyNotifiedAt";

        public async Task<ThaaliSizeChangeRequest?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM ThaaliSizeChangeRequests WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<ThaaliSizeChangeRequest>(sql, new { Id = id });
        }

        public async Task<IEnumerable<ThaaliSizeChangeRequest>> GetByFamilyIdAsync(ulong familyId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM ThaaliSizeChangeRequests WHERE FamilyId = @FamilyId ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<ThaaliSizeChangeRequest>(sql, new { FamilyId = familyId });
        }

        public async Task<IEnumerable<ThaaliSizeChangeRequest>> GetAllAsync(ThaaliSizeChangeRequestStatus? status)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM ThaaliSizeChangeRequests
                WHERE (@Status IS NULL OR Status = @Status)
                ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<ThaaliSizeChangeRequest>(sql, new { Status = status?.ToString() });
        }

        public async Task<int> CountPendingAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM ThaaliSizeChangeRequests WHERE Status = 'Pending';";
            return await connection.ExecuteScalarAsync<int>(sql);
        }

        public async Task<ulong> CreateAsync(CreateThaaliSizeChangeRequestRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO ThaaliSizeChangeRequests (FamilyId, NewThaaliSizeId, EffectiveFromDate, Reason, CreatedByUserId, Status)
                VALUES (@FamilyId, @NewThaaliSizeId, @EffectiveFromDate, @Reason, @CreatedByUserId, 'Pending');
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> ApproveAsync(ulong id, ReviewThaaliSizeChangeRequestRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                var selectSql = $"SELECT {SelectColumns} FROM ThaaliSizeChangeRequests WHERE Id = @Id;";
                var existing = await connection.QuerySingleOrDefaultAsync<ThaaliSizeChangeRequest>(selectSql, new { Id = id }, transaction);
                if (existing is null || existing.Status != ThaaliSizeChangeRequestStatus.Pending)
                {
                    transaction.Rollback();
                    return false;
                }

                // Same three-step size-change write FamilyRepository.ChangeThaaliSizeAsync
                // already used for the (now Admin-only) direct endpoint: close the
                // currently-open history row, open a new one dated EffectiveFromDate, and
                // update the family's current size -- all before marking this request Approved.
                const string closeHistorySql = @"
                    UPDATE FamilySizeHistory
                    SET EffectiveToDate = @EffectiveToDate
                    WHERE FamilyId = @FamilyId AND EffectiveToDate IS NULL;";
                await connection.ExecuteAsync(closeHistorySql, new
                {
                    FamilyId = existing.FamilyId,
                    EffectiveToDate = existing.EffectiveFromDate.AddDays(-1)
                }, transaction);

                const string insertHistorySql = @"
                    INSERT INTO FamilySizeHistory (FamilyId, ThaaliSizeId, EffectiveFromDate, ChangedByUserId)
                    VALUES (@FamilyId, @ThaaliSizeId, @EffectiveFromDate, @ChangedByUserId);";
                await connection.ExecuteAsync(insertHistorySql, new
                {
                    FamilyId = existing.FamilyId,
                    ThaaliSizeId = existing.NewThaaliSizeId,
                    EffectiveFromDate = existing.EffectiveFromDate,
                    ChangedByUserId = request.ReviewedByAdminUserId
                }, transaction);

                const string updateFamilySql = "UPDATE Families SET ThaaliSizeId = @ThaaliSizeId WHERE Id = @FamilyId;";
                await connection.ExecuteAsync(updateFamilySql, new
                {
                    FamilyId = existing.FamilyId,
                    ThaaliSizeId = existing.NewThaaliSizeId
                }, transaction);

                const string updateRequestSql = @"
                    UPDATE ThaaliSizeChangeRequests
                    SET Status = 'Approved', ReviewedByAdminUserId = @ReviewedByAdminUserId, ReviewedAt = UTC_TIMESTAMP(), AdminNote = @AdminNote
                    WHERE Id = @Id;";
                await connection.ExecuteAsync(updateRequestSql, new { Id = id, request.ReviewedByAdminUserId, request.AdminNote }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> RejectAsync(ulong id, ReviewThaaliSizeChangeRequestRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE ThaaliSizeChangeRequests
                SET Status = 'Rejected', ReviewedByAdminUserId = @ReviewedByAdminUserId, ReviewedAt = UTC_TIMESTAMP(), AdminNote = @AdminNote
                WHERE Id = @Id AND Status = 'Pending';";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.ReviewedByAdminUserId, request.AdminNote });
            return rows > 0;
        }

        public async Task<bool> MarkNotifiedAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE ThaaliSizeChangeRequests SET FamilyNotifiedAt = UTC_TIMESTAMP() WHERE Id = @Id AND FamilyNotifiedAt IS NULL;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
