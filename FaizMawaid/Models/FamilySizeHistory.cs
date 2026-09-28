namespace FaizMawaid.Models
{
    /// <summary>
    /// Maps to FamilySizeHistory -- one row per thaali-size change for a family.
    /// EffectiveToDate == null means this row is the currently active size.
    /// </summary>
    public class FamilySizeHistory
    {
        public ulong Id { get; set; }
        public ulong FamilyId { get; set; }
        public byte ThaaliSizeId { get; set; }
        public DateOnly EffectiveFromDate { get; set; }
        public DateOnly? EffectiveToDate { get; set; }
        public ulong ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
