namespace FaizMawaid.Models
{
    /// <summary>
    /// A reusable "canned" meal description an Admin defines once (e.g. "Khichdi & Kadhi")
    /// so a repeat meal can be dropped into the Meal Plans form instead of retyped. Purely
    /// a convenience library -- MealPlans.MealDescription always stores its own copy of the
    /// text, so deleting a template never affects any meal plan already built from it.
    /// </summary>
    public class MealPlanTemplate
    {
        public ushort Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MealDescription { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
