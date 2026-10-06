namespace FaizMawaid.Models.Dtos
{
    public class CreateDeliveryPersonRequest
    {
        /// <summary>One or more Areas this person serves -- at least one is required.</summary>
        public List<byte> AreaIds { get; set; } = new();
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public ulong CreatedByUserId { get; set; }
    }

    public class UpdateDeliveryPersonRequest
    {
        /// <summary>One or more Areas this person serves -- at least one is required.</summary>
        public List<byte> AreaIds { get; set; } = new();
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
