using System.Data;
using Dapper;
using FaizMawaid.Data;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Utils;

namespace FaizMawaid.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        private static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        public ReportRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<DailyThaaliCountResponse> GetDailyThaaliCountAsync(DateOnly date)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();

            const string nonServingSql = "SELECT COUNT(1) FROM NonServingDays WHERE TheDate = @Date;";
            var isNonServingDay = await connection.ExecuteScalarAsync<int>(nonServingSql, new { Date = date }) > 0;
            var isServingDay = date.DayOfWeek != DayOfWeek.Sunday && !MisriCalendar.IsRamadan(date) && !isNonServingDay;

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
                    IsServingDay = d.DayOfWeek != DayOfWeek.Sunday && !MisriCalendar.IsRamadan(d) && !nonServingDates.Contains(d),
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

        /// <summary>How many thaalis actually went out, per month, per Area -- every approved/active
        /// family (sub-families included, same filter as GetDailyThaaliCountAsync -- a sub-family row
        /// is itself Approved/Active with its own AreaId, so it flows through with no extra code)
        /// minus anyone with an active cancellation that day, summed across every serving day in
        /// [fromDate, toDate].</summary>
        public async Task<MonthlyAreaReportResponse> GetMonthlyThaaliDistributedByAreaAsync(DateOnly fromDate, DateOnly toDate)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();

            var areas = await LoadAreaColumnsAsync(connection);

            const string familiesSql = "SELECT Id, AreaId FROM Families WHERE RegistrationStatus = 'Approved' AND IsActive = 1;";
            var families = (await connection.QueryAsync<FamilyAreaRow>(familiesSql)).ToList();

            const string nonServingSql = "SELECT TheDate FROM NonServingDays WHERE TheDate BETWEEN @FromDate AND @ToDate;";
            var nonServingDates = (await connection.QueryAsync<DateOnly>(
                nonServingSql, new { FromDate = fromDate, ToDate = toDate })).ToHashSet();

            const string cancellationsSql = @"
                SELECT FamilyId, StartDate, EndDate
                FROM ThaaliCancellations
                WHERE Status = 'Active' AND StartDate <= @ToDate AND EndDate >= @FromDate;";
            var cancellations = (await connection.QueryAsync<FamilyDateRangeRow>(
                cancellationsSql, new { FromDate = fromDate, ToDate = toDate })).ToList();
            var cancelledFamiliesByDate = BuildCancelledFamiliesByDate(cancellations, fromDate, toDate);

            var months = BuildMonthRows(fromDate, toDate, areas);
            var rowsByMonthKey = months.ToDictionary(r => (r.Year, r.Month));

            for (var d = fromDate; d <= toDate; d = d.AddDays(1))
            {
                var isServingDay = d.DayOfWeek != DayOfWeek.Sunday && !MisriCalendar.IsRamadan(d) && !nonServingDates.Contains(d);
                if (!isServingDay)
                {
                    continue;
                }

                cancelledFamiliesByDate.TryGetValue(d, out var cancelledToday);
                var row = rowsByMonthKey[(d.Year, d.Month)];

                foreach (var family in families)
                {
                    if (cancelledToday is not null && cancelledToday.Contains(family.Id))
                    {
                        continue;
                    }
                    var key = ReportAreaKey.For(family.AreaId);
                    row.CountsByArea[key]++;
                    row.Total++;
                }
            }

            return new MonthlyAreaReportResponse
            {
                FromDate = fromDate,
                ToDate = toDate,
                Areas = areas,
                Months = months,
                GrandTotal = months.Sum(r => r.Total)
            };
        }

        /// <summary>How many thaali cancellations landed on a day, per month, per Area, across
        /// [fromDate, toDate] -- a family cancelled for 5 days in the window counts 5 times, matching
        /// how GetCancelledThaaliRangeAsync already counts (each day not prepared is one less thaali).</summary>
        public async Task<MonthlyAreaReportResponse> GetMonthlyThaaliCancelledByAreaAsync(DateOnly fromDate, DateOnly toDate)
        {
            using IDbConnection connection = _connectionFactory.CreateConnection();

            var areas = await LoadAreaColumnsAsync(connection);

            const string sql = @"
                SELECT tc.FamilyId AS FamilyId, f.AreaId AS AreaId, tc.StartDate AS StartDate, tc.EndDate AS EndDate
                FROM ThaaliCancellations tc
                JOIN Families f ON f.Id = tc.FamilyId
                WHERE tc.Status = 'Active' AND f.IsActive = 1
                    AND tc.StartDate <= @ToDate AND tc.EndDate >= @FromDate;";
            var cancellations = (await connection.QueryAsync<AreaCancellationRow>(
                sql, new { FromDate = fromDate, ToDate = toDate })).ToList();

            var months = BuildMonthRows(fromDate, toDate, areas);
            var rowsByMonthKey = months.ToDictionary(r => (r.Year, r.Month));

            foreach (var c in cancellations)
            {
                var start = c.StartDate < fromDate ? fromDate : c.StartDate;
                var end = c.EndDate > toDate ? toDate : c.EndDate;
                var key = ReportAreaKey.For(c.AreaId);
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    if (!rowsByMonthKey.TryGetValue((d.Year, d.Month), out var row))
                    {
                        continue;
                    }
                    row.CountsByArea[key]++;
                    row.Total++;
                }
            }

            return new MonthlyAreaReportResponse
            {
                FromDate = fromDate,
                ToDate = toDate,
                Areas = areas,
                Months = months,
                GrandTotal = months.Sum(r => r.Total)
            };
        }

        /// <summary>Every Area the Admin has defined (in SortOrder), plus a trailing "Unassigned / No Area" column.</summary>
        private static async Task<List<ReportAreaColumn>> LoadAreaColumnsAsync(IDbConnection connection)
        {
            const string sql = "SELECT Id AS AreaId, Name AS AreaName FROM Areas ORDER BY SortOrder;";
            var areas = (await connection.QueryAsync<ReportAreaColumn>(sql)).ToList();
            areas.Add(new ReportAreaColumn { AreaId = null, AreaName = "Unassigned / No Area" });
            return areas;
        }

        /// <summary>One empty (all-zero) row per calendar month spanned by [fromDate, toDate], pre-seeded
        /// with every column in <paramref name="areas"/> so the client always gets a complete pivot grid
        /// even for a month/Area with zero thaalis.</summary>
        private static List<MonthlyAreaCountRow> BuildMonthRows(DateOnly fromDate, DateOnly toDate, List<ReportAreaColumn> areas)
        {
            var rows = new List<MonthlyAreaCountRow>();
            var cursor = new DateOnly(fromDate.Year, fromDate.Month, 1);
            var end = new DateOnly(toDate.Year, toDate.Month, 1);
            while (cursor <= end)
            {
                var row = new MonthlyAreaCountRow
                {
                    Year = cursor.Year,
                    Month = cursor.Month,
                    MonthLabel = $"{MonthNames[cursor.Month - 1]} {cursor.Year}"
                };
                foreach (var area in areas)
                {
                    row.CountsByArea[ReportAreaKey.For(area.AreaId)] = 0;
                }
                rows.Add(row);
                cursor = cursor.AddMonths(1);
            }
            return rows;
        }

        /// <summary>FamilyId -> every date in [fromDate, toDate] it had an active cancellation, expanded
        /// from the (usually much shorter) list of cancellation date ranges.</summary>
        private static Dictionary<DateOnly, HashSet<ulong>> BuildCancelledFamiliesByDate(
            List<FamilyDateRangeRow> cancellations, DateOnly fromDate, DateOnly toDate)
        {
            var byDate = new Dictionary<DateOnly, HashSet<ulong>>();
            foreach (var c in cancellations)
            {
                var start = c.StartDate < fromDate ? fromDate : c.StartDate;
                var end = c.EndDate > toDate ? toDate : c.EndDate;
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    if (!byDate.TryGetValue(d, out var set))
                    {
                        set = new HashSet<ulong>();
                        byDate[d] = set;
                    }
                    set.Add(c.FamilyId);
                }
            }
            return byDate;
        }

        private class FamilyAreaRow
        {
            public ulong Id { get; set; }
            public byte? AreaId { get; set; }
        }

        private class FamilyDateRangeRow
        {
            public ulong FamilyId { get; set; }
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
        }

        private class AreaCancellationRow
        {
            public ulong FamilyId { get; set; }
            public byte? AreaId { get; set; }
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
        }
    }
}
