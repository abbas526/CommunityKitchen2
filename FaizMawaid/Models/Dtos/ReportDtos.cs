namespace FaizMawaid.Models.Dtos
{
    public class DailyThaaliCountItem
    {
        public byte ThaaliSizeId { get; set; }
        public string ThaaliSizeName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DailyThaaliCountResponse
    {
        public DateOnly Date { get; set; }
        public bool IsServingDay { get; set; }
        /// <summary>True when this date is a Special Day -- everyone (including families that don't take the regular meal) is counted, and it counts as a serving day even on a Sunday/Ramadan/non-serving day.</summary>
        public bool IsSpecialDay { get; set; }
        public string? SpecialDayName { get; set; }
        public int TotalThaalis { get; set; }
        public List<DailyThaaliCountItem> ByThaaliSize { get; set; } = new();
    }

    public class CancelledThaaliItem
    {
        public ulong FamilyId { get; set; }
        /// <summary>The family head's Sabil Number. For a sub-family (no login/user of its own), this is the parent family's Sabil Number.</summary>
        public string? SabilNumber { get; set; }
        public string? Address { get; set; }
        /// <summary>Set only when this row is a sub-family, to distinguish it from its parent in the list.</summary>
        public string? SubFamilyLabel { get; set; }
        public string ThaaliSizeName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        /// <summary>The cancellation's own start/end -- may span more days than the single date this item is grouped under.</summary>
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        /// <summary>False for a family that only receives meals on Special Days -- its cancellation only counts on a Special Day.</summary>
        public bool TakesRegularMeal { get; set; } = true;
    }

    /// <summary>All families cancelled on one specific date within a requested range.</summary>
    public class CancelledThaaliDayGroup
    {
        public DateOnly Date { get; set; }
        public bool IsServingDay { get; set; }
        public bool IsSpecialDay { get; set; }
        public string? SpecialDayName { get; set; }
        public List<CancelledThaaliItem> Cancellations { get; set; } = new();
    }

    public class CancelledThaaliRangeResponse
    {
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        /// <summary>Sum of cancellations across every day in the range -- a family cancelled for 3 days in the window counts 3 times, since that's 3 separate thaalis not being prepared.</summary>
        public int TotalCancelledInstances { get; set; }
        public List<CancelledThaaliDayGroup> Days { get; set; } = new();
    }

    // ---------------------------------------------------------------------
    // Month-wise / Area-wise reports (added 2026-10-02)
    // ---------------------------------------------------------------------

    /// <summary>Maps an Area's byte Id (null = no Area assigned) to the same string key used as a
    /// JSON object property name in MonthlyAreaCountRow.CountsByArea, so repository code and the
    /// client both address a count by the same key without juggling two different shapes.</summary>
    public static class ReportAreaKey
    {
        public const string Unassigned = "unassigned";
        public static string For(byte? areaId) => areaId.HasValue ? areaId.Value.ToString() : Unassigned;
    }

    /// <summary>One column header in an Area-wise report pivot table -- every Area the Admin has
    /// defined (in SortOrder), plus a trailing "Unassigned / No Area" column for families with no
    /// AreaId set. AreaId is null only for that trailing column; match it to a row's count via
    /// ReportAreaKey.For(AreaId).</summary>
    public class ReportAreaColumn
    {
        public byte? AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }

    /// <summary>One row (one calendar month) of an Area-wise monthly report.</summary>
    public class MonthlyAreaCountRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        /// <summary>Ready-to-display label, e.g. "January 2026".</summary>
        public string MonthLabel { get; set; } = string.Empty;
        /// <summary>Key = ReportAreaKey.For(column.AreaId) for every column in the response's Areas list -- always present (zero-filled) even for a month/Area with nothing to report.</summary>
        public Dictionary<string, int> CountsByArea { get; set; } = new();
        public int Total { get; set; }
    }

    /// <summary>
    /// Shared shape for both Area-wise monthly reports:
    /// - Thaalis distributed (GET /api/reports/monthly-thaali-distributed) -- how many thaalis
    ///   actually went out, per month, per Area, counting every serving day in the range.
    /// - Thaalis cancelled (GET /api/reports/monthly-thaali-cancelled) -- how many thaali
    ///   cancellations landed on a day in the range, per month, per Area (a family cancelled for
    ///   5 days counts 5 times, matching how CancelledThaaliRangeResponse already counts).
    /// A pivot table: one row per calendar month spanned by [FromDate, ToDate], one column per
    /// Area (+ a trailing "Unassigned" column).
    /// </summary>
    public class MonthlyAreaReportResponse
    {
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public List<ReportAreaColumn> Areas { get; set; } = new();
        public List<MonthlyAreaCountRow> Months { get; set; } = new();
        public int GrandTotal { get; set; }
    }
}
