namespace FaizMawaid.Models
{
    /// <summary>Maps to the DeliveryPersons table -- the person who delivers/serves the
    /// Thaali to homes in a particular Area (as opposed to a family that picks its own
    /// thaali up from the kitchen, for whom no Delivery Person applies).</summary>
    public class DeliveryPerson
    {
        public ulong Id { get; set; }
        public byte AreaId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public ulong CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
