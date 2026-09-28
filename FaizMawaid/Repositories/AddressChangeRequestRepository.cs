using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class AddressChangeRequestRepository : IAddressChangeRequestRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public AddressChangeRequestRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private const string SelectColumns = @"
            Id, FamilyId, NewAddress, NewAreaId, EffectiveDate, Reason, Status, CreatedByUserId, CreatedAt,
            ReviewedByAdminUserId, ReviewedAt, AdminNote, FamilyNotifiedAt";

        public async Task<AddressChangeRequest?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM AddressChangeRequests WHERE Id = @Id;";
            return await connection.QuerySingleOrDefaultAsync<AddressChangeRequest>(sql, new { Id = id });
        }

        public async Task<IEnumerable<AddressChangeRequest>> GetByFamilyIdAsync(ulong familyId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM AddressChangeRequests WHERE FamilyId = @FamilyId ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<AddressChangeRequest>(sql, new { FamilyId = familyId });
        }

        public async Task<IEnumerable<AddressChangeRequest>> GetAllAsync(AddressChangeRequestStatus? status)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $@"SELECT {SelectColumns} FROM AddressChangeRequests
                WHERE (@Status IS NULL OR Status = @Status)
                ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<AddressChangeRequest>(sql, new { Status = status?.ToString() });
        }

        public async Task<int> CountPendingAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(1) FROM AddressChangeRequests WHERE Status = 'Pending';";
            return await connection.ExecuteScalarAsync<int>(sql);
        }

        public async Task<ulong> CreateAsync(CreateAddressChangeRequestRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO AddressChangeRequests (FamilyId, NewAddress, NewAreaId, EffectiveDate, Reason, CreatedByUserId, Status)
                VALUES (@FamilyId, @NewAddress, @NewAreaId, @EffectiveDate, @Reason, @CreatedByUserId, 'Pending');
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<ulong>(sql, request);
        }

        public async Task<bool> ApproveAsync(ulong id, ReviewAddressChangeRequestRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                var selectSql = $"SELECT {SelectColumns} FROM AddressChangeRequests WHERE Id = @Id;";
                var existing = await connection.QuerySingleOrDefaultAsync<AddressChangeRequest>(selectSql, new { Id = id }, transaction);
                if (existing is null || existing.Status != AddressChangeRequestStatus.Pending)
                {
                    transaction.Rollback();
                    return false;
                }

                const string updateFamilySql = "UPDATE Families SET Address = @NewAddress, AreaId = @NewAreaId WHERE Id = @FamilyId;";
                await connection.ExecuteAsync(updateFamilySql, new { existing.NewAddress, existing.NewAreaId, FamilyId = existing.FamilyId }, transaction);

                const string updateRequestSql = @"
                    UPDATE AddressChangeRequests
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

        public async Task<bool> RejectAsync(ulong id, ReviewAddressChangeRequestRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                UPDATE AddressChangeRequests
                SET Status = 'Rejected', ReviewedByAdminUserId = @ReviewedByAdminUserId, ReviewedAt = UTC_TIMESTAMP(), AdminNote = @AdminNote
                WHERE Id = @Id AND Status = 'Pending';";
            var rows = await connection.ExecuteAsync(sql, new { Id = id, request.ReviewedByAdminUserId, request.AdminNote });
            return rows > 0;
        }

        public async Task<bool> MarkNotifiedAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = "UPDATE AddressChangeRequests SET FamilyNotifiedAt = UTC_TIMESTAMP() WHERE Id = @Id AND FamilyNotifiedAt IS NULL;";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }
    }
}
