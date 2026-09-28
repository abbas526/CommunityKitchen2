namespace FaizMawaid.Models
{
    /// <summary>Maps to the Roles table. Seeded rows: 1 = Admin, 2 = FamilyHead.</summary>
    public class Role
    {
        public byte Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
