namespace FaizMawaid.Models.Dtos
{
    public class CreateDeliveryPersonRequest
    {
        public byte AreaId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public ulong CreatedByUserId { get; set; }
    }

    public class UpdateDeliveryPersonRequest
    {
        public byte AreaId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
