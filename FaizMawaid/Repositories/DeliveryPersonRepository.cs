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
            Id, FullName, MobileNumber, IsActive, CreatedByUserId, CreatedAt, UpdatedAt";

        public async Task<IEnumerable<DeliveryPerson>> GetAllAsync()
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM DeliveryPersons ORDER BY FullName;";
            var persons = (await connection.QueryAsync<DeliveryPerson>(sql)).ToList();
            await AttachAreaIdsAsync(connection, persons);
            return persons;
        }

        public async Task<DeliveryPerson?> GetByIdAsync(ulong id)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            var sql = $"SELECT {SelectColumns} FROM DeliveryPersons WHERE Id = @Id;";
            var person = await connection.QuerySingleOrDefaultAsync<DeliveryPerson>(sql, new { Id = id });
            if (person is not null)
            {
                await AttachAreaIdsAsync(connection, new List<DeliveryPerson> { person });
            }
            return person;
        }

        public async Task<IEnumerable<DeliveryPerson>> GetActiveByAreaIdAsync(byte areaId)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            const string sql = @"
                SELECT dp.Id, dp.FullName, dp.MobileNumber, dp.IsActive, dp.CreatedByUserId, dp.CreatedAt, dp.UpdatedAt
                FROM DeliveryPersons dp
                JOIN DeliveryPersonAreas dpa ON dpa.DeliveryPersonId = dp.Id
                WHERE dpa.AreaId = @AreaId AND dp.IsActive = 1
                ORDER BY dp.FullName;";
            var persons = (await connection.QueryAsync<DeliveryPerson>(sql, new { AreaId = areaId })).ToList();
            await AttachAreaIdsAsync(connection, persons);
            return persons;
        }

        public async Task<ulong> CreateAsync(CreateDeliveryPersonRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"
                    INSERT INTO DeliveryPersons (FullName, MobileNumber, IsActive, CreatedByUserId)
                    VALUES (@FullName, @MobileNumber, 1, @CreatedByUserId);
                    SELECT LAST_INSERT_ID();";
                var id = await connection.ExecuteScalarAsync<ulong>(
                    sql, new { request.FullName, request.MobileNumber, request.CreatedByUserId }, transaction);

                await InsertAreasAsync(connection, transaction, id, request.AreaIds);

                transaction.Commit();
                return id;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateAsync(ulong id, UpdateDeliveryPersonRequest request)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();
            using IDbTransaction transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"
                    UPDATE DeliveryPersons
                    SET FullName = @FullName, MobileNumber = @MobileNumber, IsActive = @IsActive, UpdatedAt = UTC_TIMESTAMP()
                    WHERE Id = @Id;";
                var rows = await connection.ExecuteAsync(
                    sql, new { Id = id, request.FullName, request.MobileNumber, request.IsActive }, transaction);
                if (rows == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                // Replace the whole set of Areas this person serves with what was submitted.
                await connection.ExecuteAsync("DELETE FROM DeliveryPersonAreas WHERE DeliveryPersonId = @Id;", new { Id = id }, transaction);
                await InsertAreasAsync(connection, transaction, id, request.AreaIds);

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static async Task InsertAreasAsync(IDbConnection connection, IDbTransaction transaction, ulong deliveryPersonId, IEnumerable<byte> areaIds)
        {
            const string sql = "INSERT INTO DeliveryPersonAreas (DeliveryPersonId, AreaId) VALUES (@DeliveryPersonId, @AreaId);";
            foreach (var areaId in areaIds.Distinct())
            {
                await connection.ExecuteAsync(sql, new { DeliveryPersonId = deliveryPersonId, AreaId = areaId }, transaction);
            }
        }

        private class DeliveryPersonAreaRow
        {
            public ulong DeliveryPersonId { get; set; }
            public byte AreaId { get; set; }
        }

        /// <summary>Fills in AreaIds on each person with one extra query (not one per person).</summary>
        private static async Task AttachAreaIdsAsync(IDbConnection connection, List<DeliveryPerson> persons)
        {
            if (persons.Count == 0)
            {
                return;
            }
            const string sql = "SELECT DeliveryPersonId, AreaId FROM DeliveryPersonAreas WHERE DeliveryPersonId IN @Ids ORDER BY AreaId;";
            var links = await connection.QueryAsync<DeliveryPersonAreaRow>(sql, new { Ids = persons.Select(p => p.Id).ToArray() });
            var byPerson = links.GroupBy(l => l.DeliveryPersonId).ToDictionary(g => g.Key, g => g.Select(l => l.AreaId).ToList());
            foreach (var person in persons)
            {
                person.AreaIds = byPerson.TryGetValue(person.Id, out var areaIds) ? areaIds : new List<byte>();
            }
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
