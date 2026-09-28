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
    }

    /// <summary>All families cancelled on one specific date within a requested range.</summary>
    public class CancelledThaaliDayGroup
    {
        public DateOnly Date { get; set; }
        public bool IsServingDay { get; set; }
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
}
