namespace FaizMawaid.Models.Dtos
{
    public class CreateThaaliSizeRequest
    {
        public string Name { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }

    public class UpdateThaaliSizeRequest
    {
        public string Name { get; set; } = string.Empty;
        public byte SortOrder { get; set; }
    }
}
