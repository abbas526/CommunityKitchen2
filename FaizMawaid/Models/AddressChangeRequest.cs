namespace FaizMawaid.Models
{
    public enum AddressChangeRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    /// <summary>
    /// Maps to AddressChangeRequests -- a Family Head's request to change their Address
    /// and/or Area (which Delivery Person serves them -- see Area.cs/DeliveryPerson.cs),
    /// with a target EffectiveDate the family gives as guidance for when Admin should act
    /// on it. Approving writes NewAddress/NewAreaId straight into Families immediately --
    /// there's no deferred/scheduled application, since Admin reviews these daily anyway.
    /// FamilyNotifiedAt tracks whether the family has seen a one-time flash about the
    /// outcome yet (set the first time their dashboard shows it).
    /// </summary>
    public class AddressChangeRequest
    {
        public ulong Id { get; set; }
        public ulong FamilyId { get; set; }
        public string NewAddress { get; set; } = string.Empty;
        public byte? NewAreaId { get; set; }
        public DateOnly EffectiveDate { get; set; }
        public string? Reason { get; set; }
        public AddressChangeRequestStatus Status { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ulong? ReviewedByAdminUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? AdminNote { get; set; }
        public DateTime? FamilyNotifiedAt { get; set; }
    }
}
