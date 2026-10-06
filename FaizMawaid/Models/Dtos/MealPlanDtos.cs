namespace FaizMawaid.Models.Dtos
{
    public class CreateMealPlanRequest
    {
        public DateOnly MealDate { get; set; }
        public string MealDescription { get; set; } = string.Empty;
        public bool IsSpecialDay { get; set; }
        public string? SpecialDayName { get; set; }
        public ulong CreatedByUserId { get; set; }
    }

    public class UpdateMealPlanRequest
    {
        public string MealDescription { get; set; } = string.Empty;
        /// <summary>Null = leave the Special Day flag/name as they are (so an older client or an Excel sheet without that column can't accidentally clear it).</summary>
        public bool? IsSpecialDay { get; set; }
        public string? SpecialDayName { get; set; }
        public ulong UpdatedByUserId { get; set; }
    }

    /// <summary>One row of a bulk meal-plan upload -- the client parses an Excel file into these client-side and posts the whole batch.</summary>
    public class BulkMealPlanItem
    {
        public DateOnly MealDate { get; set; }
        public string MealDescription { get; set; } = string.Empty;
        /// <summary>Optional Special Day (Yes/No) column; null/blank = unchanged for an existing date, No for a new one.</summary>
        public bool? IsSpecialDay { get; set; }
        public string? SpecialDayName { get; set; }
    }

    public class BulkMealPlanRequest
    {
        public List<BulkMealPlanItem> Items { get; set; } = new();
        public ulong CreatedByUserId { get; set; }
    }

    /// <summary>Status is "Created", "Updated" (a meal already existed for that date and was overwritten), or "Skipped" (see Message).</summary>
    public class BulkMealPlanRowResult
    {
        public string MealDate { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class BulkMealPlanResponse
    {
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<BulkMealPlanRowResult> Rows { get; set; } = new();
    }
}
