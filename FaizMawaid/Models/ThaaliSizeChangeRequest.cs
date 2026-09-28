namespace FaizMawaid.Models
{
    public enum ThaaliSizeChangeRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    /// <summary>
    /// Maps to ThaaliSizeChangeRequests -- a Family Head's request to change their
    /// Thaali size, with a target EffectiveFromDate. Unlike AddressChangeRequest's
    /// EffectiveDate (guidance only), this date is used for real: approving a request
    /// closes the family's currently-open FamilySizeHistory row, opens a new one dated
    /// EffectiveFromDate, and updates Families.ThaaliSizeId -- exactly what the direct
    /// PUT /api/families/{id}/thaali-size endpoint already did, just gated behind an
    /// Admin decision now instead of applying immediately when the family submits it.
    /// FamilyNotifiedAt tracks whether the family has seen a one-time flash about the
    /// outcome yet (set the first time their dashboard shows it).
    /// </summary>
    public class ThaaliSizeChangeRequest
    {
        public ulong Id { get; set; }
        public ulong FamilyId { get; set; }
        public byte NewThaaliSizeId { get; set; }
        public DateOnly EffectiveFromDate { get; set; }
        public string? Reason { get; set; }
        public ThaaliSizeChangeRequestStatus Status { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ulong? ReviewedByAdminUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? AdminNote { get; set; }
        public DateTime? FamilyNotifiedAt { get; set; }
    }
}
