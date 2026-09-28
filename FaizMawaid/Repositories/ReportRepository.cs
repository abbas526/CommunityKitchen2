using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;

namespace FaizMawaid.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ReportRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DailyThaaliCountResponse> GetDailyThaaliCountAsync(DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();

            const string nonServingSql = "SELECT COUNT(1) FROM NonServingDays WHERE TheDate = @Date;";
            var isNonServingDay = await connection.ExecuteScalarAsync<int>(nonServingSql, new { Date = date }) > 0;
            var isServingDay = date.DayOfWeek != DayOfWeek.Sunday && !isNonServingDay;

            const string sql = @"
                SELECT ts.Id AS ThaaliSizeId, ts.Name AS ThaaliSizeName, COUNT(f.Id) AS Count
                FROM ThaaliSizes ts
                LEFT JOIN Families f
                    ON f.ThaaliSizeId = ts.Id
                    AND f.RegistrationStatus = 'Approved'
                    AND f.IsActive = 1
                    AND f.Id NOT IN (
                        SELECT FamilyId FROM ThaaliCancellations
                        WHERE Status = 'Active' AND StartDate <= @Date AND EndDate >= @Date
                    )
                GROUP BY ts.Id, ts.Name, ts.SortOrder
                ORDER BY ts.SortOrder;";
            var rows = (await connection.QueryAsync<DailyThaaliCountItem>(sql, new { Date = date })).ToList();

            return new DailyThaaliCountResponse
            {
                Date = date,
                IsServingDay = isServingDay,
                TotalThaalis = rows.Sum(r => r.Count),
                ByThaaliSize = rows
            };
        }

        public async Task<CancelledThaaliRangeResponse> GetCancelledThaaliRangeAsync(DateOnly fromDate, DateOnly toDate)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();

            // One query for every cancellation that overlaps the range at all (not per-day --
            // avoids a round trip per date), then expanded/grouped by day in memory below.
            const string cancellationsSql = @"
                SELECT
                    f.Id AS FamilyId,
                    COALESCE(headUser.SabilNumber, parentHeadUser.SabilNumber) AS SabilNumber,
                    f.Address AS Address,
                    f.SubFamilyLabel AS SubFamilyLabel,
                    ts.Name AS ThaaliSizeName,
                    tc.Reason AS Reason,
                    tc.StartDate AS StartDate,
                    tc.EndDate AS EndDate
                FROM ThaaliCancellations tc
                JOIN Families f ON f.Id = tc.FamilyId
                JOIN ThaaliSizes ts ON ts.Id = f.ThaaliSizeId
                LEFT JOIN Users headUser ON headUser.Id = f.FamilyHeadUserId
                LEFT JOIN Families parentFamily ON parentFamily.Id = f.ParentFamilyId
                LEFT JOIN Users parentHeadUser ON parentHeadUser.Id = parentFamily.FamilyHeadUserId
                WHERE tc.Status = 'Active'
                    AND tc.StartDate <= @ToDate AND tc.EndDate >= @FromDate
                    AND f.IsActive = 1
                ORDER BY f.Address IS NULL, f.Address, SabilNumber;";
            var cancellations = (await connection.QueryAsync<CancelledThaaliItem>(
                cancellationsSql, new { FromDate = fromDate, ToDate = toDate })).ToList();

            const string nonServingSql = "SELECT TheDate FROM NonServingDays WHERE TheDate BETWEEN @FromDate AND @ToDate;";
            var nonServingDates = (await connection.QueryAsync<DateOnly>(
                nonServingSql, new { FromDate = fromDate, ToDate = toDate })).ToHashSet();

            var days = new List<CancelledThaaliDayGroup>();
            var totalInstances = 0;
            for (var d = fromDate; d <= toDate; d = d.AddDays(1))
            {
                var dayCancellations = cancellations.Where(c => c.StartDate <= d && c.EndDate >= d).ToList();
                totalInstances += dayCancellations.Count;
                days.Add(new CancelledThaaliDayGroup
                {
                    Date = d,
                    IsServingDay = d.DayOfWeek != DayOfWeek.Sunday && !nonServingDates.Contains(d),
                    Cancellations = dayCancellations
                });
            }

            return new CancelledThaaliRangeResponse
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalCancelledInstances = totalInstances,
                Days = days
            };
        }
    }
}
