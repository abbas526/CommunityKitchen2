namespace FaizMawaid.Models
{
    public enum ThaaliCancellationStatus
    {
        Active,
        Reinstated
    }

    /// <summary>
    /// Maps to ThaaliCancellations -- the core "save food" record. A family is assumed
    /// to want their thaali on any serving day unless an Active row here covers that date.
    /// </summary>
    public class ThaaliCancellation
    {
        public ulong Id { get; set; }
        public ulong FamilyId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string? Reason { get; set; }
        public ThaaliCancellationStatus Status { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReinstatedAt { get; set; }
        public ulong? ReinstatedByUserId { get; set; }
    }
}
