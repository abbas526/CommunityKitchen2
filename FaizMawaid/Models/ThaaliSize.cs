namespace FaizMawaid.Models
{
    /// <summary>Maps to the ThaaliSizes table -- the 3-4 sizes an Admin defines.</summary>
    public class ThaaliSize
    {
        public byte Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }
}
