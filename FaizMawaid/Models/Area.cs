namespace FaizMawaid.Models
{
    /// <summary>Maps to the Areas table -- the Areas/Towers an Admin defines. A family can
    /// optionally pick one at registration, and each Area can have one or more Delivery
    /// Persons assigned (see DeliveryPerson.cs).</summary>
    public class Area
    {
        public byte Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }
}
