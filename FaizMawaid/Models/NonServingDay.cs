namespace FaizMawaid.Models
{
    /// <summary>
    /// Maps to NonServingDays -- the *extra* no-service dates in a year, beyond the
    /// fixed weekly Sunday rule (which is applied in code, not stored here).
    /// </summary>
    public class NonServingDay
    {
        public uint Id { get; set; }
        public DateOnly TheDate { get; set; }
        public string? Reason { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
