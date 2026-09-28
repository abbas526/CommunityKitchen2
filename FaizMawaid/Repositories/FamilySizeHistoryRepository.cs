using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class FamilySizeHistoryRepository : IFamilySizeHistoryRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public FamilySizeHistoryRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<FamilySizeHistory>> GetByFamilyIdAsync(ulong familyId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT Id, FamilyId, ThaaliSizeId, EffectiveFromDate, EffectiveToDate, ChangedByUserId, ChangedAt
                FROM FamilySizeHistory
                WHERE FamilyId = @FamilyId
                ORDER BY EffectiveFromDate DESC;";
            return await connection.QueryAsync<FamilySizeHistory>(sql, new { FamilyId = familyId });
        }

        public async Task<FamilySizeHistory?> GetSizeOnDateAsync(ulong familyId, DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT Id, FamilyId, ThaaliSizeId, EffectiveFromDate, EffectiveToDate, ChangedByUserId, ChangedAt
                FROM FamilySizeHistory
                WHERE FamilyId = @FamilyId
                  AND EffectiveFromDate <= @Date
                  AND (EffectiveToDate IS NULL OR EffectiveToDate >= @Date)
                LIMIT 1;";
            return await connection.QuerySingleOrDefaultAsync<FamilySizeHistory>(sql, new { FamilyId = familyId, Date = date });
        }
    }
}
