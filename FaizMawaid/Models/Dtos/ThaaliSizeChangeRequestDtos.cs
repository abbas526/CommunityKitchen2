namespace FaizMawaid.Models.Dtos
{
    /// <summary>A Family Head requesting a Thaali size change, effective on a given date.</summary>
    public class CreateThaaliSizeChangeRequestRequest
    {
        public ulong FamilyId { get; set; }
        public ulong CreatedByUserId { get; set; }
        public byte NewThaaliSizeId { get; set; }
        public DateOnly EffectiveFromDate { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>An Admin approving or rejecting a request, with an optional note.</summary>
    public class ReviewThaaliSizeChangeRequestRequest
    {
        public ulong ReviewedByAdminUserId { get; set; }
        public string? AdminNote { get; set; }
    }
}
