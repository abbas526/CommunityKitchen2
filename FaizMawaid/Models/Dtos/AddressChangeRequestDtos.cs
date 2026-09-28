namespace FaizMawaid.Models.Dtos
{
    /// <summary>A Family Head requesting an Address/Area change, effective on a given date.</summary>
    public class CreateAddressChangeRequestRequest
    {
        public ulong FamilyId { get; set; }
        public ulong CreatedByUserId { get; set; }
        public string NewAddress { get; set; } = string.Empty;
        public byte? NewAreaId { get; set; }
        public DateOnly EffectiveDate { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>An Admin approving or rejecting a request, with an optional note.</summary>
    public class ReviewAddressChangeRequestRequest
    {
        public ulong ReviewedByAdminUserId { get; set; }
        public string? AdminNote { get; set; }
    }
}
