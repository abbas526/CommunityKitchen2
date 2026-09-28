namespace FaizMawaid.Models
{
    /// <summary>
    /// Maps to AppSettings -- a single-row (Id = 1) system configuration table.
    /// </summary>
    public class AppSetting
    {
        public byte Id { get; set; }
        public uint MealVisibilityDays { get; set; }
        public ulong? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
