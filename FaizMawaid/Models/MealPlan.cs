namespace FaizMawaid.Models
{
    /// <summary>
    /// Maps to MealPlans -- the meal an Admin enters ahead of time for a serving date.
    /// How many of these are actually shown to Family Members is controlled separately
    /// by AppSettings.MealVisibilityDays.
    /// </summary>
    public class MealPlan
    {
        public ulong Id { get; set; }
        public DateOnly MealDate { get; set; }
        public string MealDescription { get; set; } = string.Empty;
        /// <summary>On a Special Day EVERY approved, active family receives the meal (including families that don't take the regular meal), even on a Sunday, in Ramadan, or on a declared non-serving day.</summary>
        public bool IsSpecialDay { get; set; }
        public string? SpecialDayName { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ulong? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
