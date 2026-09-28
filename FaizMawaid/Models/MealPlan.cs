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
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ulong? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
